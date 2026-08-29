using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Apps.FirstParty;

public interface ILowStockAlertService
{
    /// <summary>Hangfire sweep: for every store with Low Stock Alerts installed + enabled, email the merchant
    /// the products at/below their threshold (throttled to at most once per ThrottleHours).</summary>
    Task<int> RunSweepAsync(CancellationToken ct = default);
    /// <summary>Run the check for the current tenant on demand (used by the "Send test" button); returns rows found.</summary>
    Task<int> RunForCurrentTenantAsync(CancellationToken ct = default);
}

/// <summary>
/// First-party app "Low Stock Alerts" (validates the App Store surface end-to-end: install → per-install
/// settings → scoped inventory read → scheduled email). Reads each installation's threshold/recipient from
/// <c>AppSettings</c>; emails the store's admin when products run low.
/// </summary>
public sealed class LowStockAlertService(
    EcommerceDbContext db, IEmailSender email, ICurrentTenantService tenant, ILogger<LowStockAlertService> log)
    : ILowStockAlertService
{
    private const string Slug = "low-stock-alerts";
    private const int DefaultThreshold = 5;
    private const int ThrottleHours = 20;
    private const int MaxRows = 50;

    public async Task<int> RunSweepAsync(CancellationToken ct = default)
    {
        var appId = await db.Apps.IgnoreQueryFilters().Where(a => a.Slug == Slug).Select(a => a.AppId).FirstOrDefaultAsync(ct);
        if (appId == 0) return 0;

        var installs = await db.AppInstallations.IgnoreQueryFilters()
            .Where(i => i.AppId == appId && i.Status == "installed")
            .Select(i => new { i.AppInstallationId, i.TenantId })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var install in installs)
        {
            using (tenant.BeginScope(install.TenantId))
            {
                try { if (await CheckAndNotifyAsync(install.AppInstallationId, install.TenantId, respectThrottle: true, ct)) sent++; }
                catch (Exception ex) { log.LogWarning(ex, "Low-stock alert failed for tenant {Tenant}.", install.TenantId); }
            }
        }
        return sent;
    }

    public async Task<int> RunForCurrentTenantAsync(CancellationToken ct = default)
    {
        var appId = await db.Apps.Where(a => a.Slug == Slug).Select(a => a.AppId).FirstOrDefaultAsync(ct);
        var inst = await db.AppInstallations.FirstOrDefaultAsync(i => i.AppId == appId && i.Status == "installed", ct);
        if (inst is null) return 0;
        await CheckAndNotifyAsync(inst.AppInstallationId, db.CurrentTenantId, respectThrottle: false, ct);
        return await LowStockCountAsync(await ThresholdAsync(inst.AppInstallationId, ct), ct);
    }

    private async Task<bool> CheckAndNotifyAsync(long installationId, long tenantId, bool respectThrottle, CancellationToken ct)
    {
        var settings = await db.AppSettings.Where(s => s.AppInstallationId == installationId).ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        if (settings.GetValueOrDefault("enabled") == "false") return false;

        if (respectThrottle && DateTime.TryParse(settings.GetValueOrDefault("lastAlertAt"), out var last)
            && last > DateTime.UtcNow.AddHours(-ThrottleHours))
            return false;

        var threshold = int.TryParse(settings.GetValueOrDefault("threshold"), out var t) && t > 0 ? t : DefaultThreshold;

        var low = await db.Products
            .Where(p => !p.IsDeleted && p.IsActive && p.Status == "Active")
            .Select(p => new { p.Name, Qty = p.InventoryRecords.Sum(i => (int?)i.AvailableQty) ?? 0 })
            .Where(x => x.Qty <= threshold)
            .OrderBy(x => x.Qty)
            .Take(MaxRows)
            .ToListAsync(ct);
        if (low.Count == 0) return false;

        var recipient = settings.GetValueOrDefault("recipientEmail");
        if (string.IsNullOrWhiteSpace(recipient)) recipient = await AdminEmailAsync(tenantId, ct);
        if (string.IsNullOrWhiteSpace(recipient)) return false;

        var storeName = await db.Tenants.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).Select(x => x.Name).FirstOrDefaultAsync(ct);
        var rows = string.Concat(low.Select(x =>
            $"<tr><td style=\"padding:4px 8px\">{System.Net.WebUtility.HtmlEncode(x.Name)}</td><td style=\"padding:4px 8px;text-align:right\">{x.Qty}</td></tr>"));
        var body = $"<p>These products at <strong>{System.Net.WebUtility.HtmlEncode(storeName)}</strong> are at or below your low-stock threshold of {threshold}:</p>"
                 + $"<table style=\"border-collapse:collapse\"><tr><th style=\"text-align:left;padding:4px 8px\">Product</th><th style=\"padding:4px 8px\">In stock</th></tr>{rows}</table>"
                 + "<p>Restock them to avoid missed sales.</p>";
        await email.SendAsync(recipient!, $"{low.Count} product(s) running low at {storeName}", body, ct);

        await UpsertSettingAsync(installationId, tenantId, "lastAlertAt", DateTime.UtcNow.ToString("o"), ct);
        log.LogInformation("Low-stock alert: emailed {Count} products for tenant {Tenant}.", low.Count, tenantId);
        return true;
    }

    private async Task<int> ThresholdAsync(long installationId, CancellationToken ct)
    {
        var v = await db.AppSettings.Where(s => s.AppInstallationId == installationId && s.Key == "threshold").Select(s => s.Value).FirstOrDefaultAsync(ct);
        return int.TryParse(v, out var t) && t > 0 ? t : DefaultThreshold;
    }

    private Task<int> LowStockCountAsync(int threshold, CancellationToken ct) =>
        db.Products.Where(p => !p.IsDeleted && p.IsActive && p.Status == "Active"
            && (p.InventoryRecords.Sum(i => (int?)i.AvailableQty) ?? 0) <= threshold).CountAsync(ct);

    private async Task<string?> AdminEmailAsync(long tenantId, CancellationToken ct) =>
        await (from u in db.Users.IgnoreQueryFilters()
               join ur in db.UserRoles on u.UserId equals ur.UserId
               join r in db.Roles on ur.RoleId equals r.RoleId
               where u.TenantId == tenantId && !u.IsDeleted && r.NormalizedName == "ADMIN" && u.Email != null
               select u.Email).FirstOrDefaultAsync(ct);

    private async Task UpsertSettingAsync(long installationId, long tenantId, string key, string value, CancellationToken ct)
    {
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.AppInstallationId == installationId && s.Key == key, ct);
        if (row is null) db.AppSettings.Add(new Data.Entities.AppSetting { TenantId = tenantId, AppInstallationId = installationId, Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        else { row.Value = value; row.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct);
    }
}
