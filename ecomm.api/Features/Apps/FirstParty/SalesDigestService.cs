using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Apps.FirstParty;

public interface ISalesDigestService
{
    /// <summary>Daily Hangfire sweep: sends each installed store its digest when one is due (by frequency).</summary>
    Task<int> RunSweepAsync(CancellationToken ct = default);
    /// <summary>Send the current tenant's digest now (the "Send now" button); returns the order count in the window.</summary>
    Task<int> RunForCurrentTenantAsync(CancellationToken ct = default);
}

/// <summary>
/// Second first-party app "Sales Digest" — a daily/weekly email summarising orders, revenue and top
/// products. Reuses the App Store install/settings plumbing; reads order history within the window.
/// </summary>
public sealed class SalesDigestService(
    EcommerceDbContext db, IEmailSender email, ICurrentTenantService tenant, ILogger<SalesDigestService> log)
    : ISalesDigestService
{
    private const string Slug = "sales-digest";
    private static readonly string[] SoldStatuses = { "Paid", "Confirmed", "Packed", "Shipped", "Delivered" };

    public async Task<int> RunSweepAsync(CancellationToken ct = default)
    {
        var appId = await db.Apps.IgnoreQueryFilters().Where(a => a.Slug == Slug).Select(a => a.AppId).FirstOrDefaultAsync(ct);
        if (appId == 0) return 0;
        var installs = await db.AppInstallations.IgnoreQueryFilters()
            .Where(i => i.AppId == appId && i.Status == "installed")
            .Select(i => new { i.AppInstallationId, i.TenantId }).ToListAsync(ct);

        var sent = 0;
        foreach (var i in installs)
            using (tenant.BeginScope(i.TenantId))
            {
                try { if (await SendAsync(i.AppInstallationId, i.TenantId, force: false, ct)) sent++; }
                catch (Exception ex) { log.LogWarning(ex, "Sales digest failed for tenant {Tenant}.", i.TenantId); }
            }
        return sent;
    }

    public async Task<int> RunForCurrentTenantAsync(CancellationToken ct = default)
    {
        var appId = await db.Apps.Where(a => a.Slug == Slug).Select(a => a.AppId).FirstOrDefaultAsync(ct);
        var inst = await db.AppInstallations.FirstOrDefaultAsync(i => i.AppId == appId && i.Status == "installed", ct);
        if (inst is null) return 0;
        var (_, count) = await BuildAndMaybeSendAsync(inst.AppInstallationId, db.CurrentTenantId, force: true, ct);
        return count;
    }

    private async Task<bool> SendAsync(long installationId, long tenantId, bool force, CancellationToken ct)
    {
        var (sent, _) = await BuildAndMaybeSendAsync(installationId, tenantId, force, ct);
        return sent;
    }

    private async Task<(bool sent, int count)> BuildAndMaybeSendAsync(long installationId, long tenantId, bool force, CancellationToken ct)
    {
        var s = await db.AppSettings.Where(x => x.AppInstallationId == installationId).ToDictionaryAsync(x => x.Key, x => x.Value, ct);
        if (!force && s.GetValueOrDefault("enabled") == "false") return (false, 0);

        var weekly = string.Equals(s.GetValueOrDefault("frequency"), "weekly", StringComparison.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        if (!force && DateTime.TryParse(s.GetValueOrDefault("lastSentAt"), out var last))
        {
            var minGap = weekly ? TimeSpan.FromDays(6.5) : TimeSpan.FromHours(20);
            if (now - last < minGap) return (false, 0);
        }

        var since = weekly ? now.AddDays(-7) : now.AddDays(-1);
        var orders = await db.Orders.AsNoTracking()
            .Where(o => SoldStatuses.Contains(o.Status) && o.PlacedAt != null && o.PlacedAt >= since)
            .Select(o => new { o.OrderId, o.TotalAmount }).ToListAsync(ct);
        var count = orders.Count;
        var revenue = orders.Sum(o => o.TotalAmount);

        if (!force && count == 0) return (false, 0);   // don't spam empty digests on the schedule

        var top = await db.OrderItems.AsNoTracking()
            .Where(oi => SoldStatuses.Contains(oi.Order!.Status) && oi.Order.PlacedAt != null && oi.Order.PlacedAt >= since)
            .GroupBy(oi => oi.ProductName)
            .Select(g => new { Name = g.Key, Qty = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Qty).Take(5).ToListAsync(ct);

        var recipient = s.GetValueOrDefault("recipientEmail");
        if (string.IsNullOrWhiteSpace(recipient)) recipient = await AdminEmailAsync(tenantId, ct);
        if (string.IsNullOrWhiteSpace(recipient)) return (false, count);

        var storeName = await db.Tenants.IgnoreQueryFilters().Where(t => t.TenantId == tenantId).Select(t => t.Name).FirstOrDefaultAsync(ct);
        var period = weekly ? "last 7 days" : "yesterday";
        var topRows = top.Count == 0 ? "<tr><td colspan=\"2\" style=\"padding:4px 8px;color:#888\">No items</td></tr>"
            : string.Concat(top.Select(x => $"<tr><td style=\"padding:4px 8px\">{System.Net.WebUtility.HtmlEncode(x.Name)}</td><td style=\"padding:4px 8px;text-align:right\">{x.Qty}</td></tr>"));
        var body = $"<p><strong>{System.Net.WebUtility.HtmlEncode(storeName)}</strong> — sales for the {period}:</p>"
                 + $"<p style=\"font-size:15px\">🧾 <strong>{count}</strong> order(s) &nbsp; · &nbsp; 💰 <strong>Rs. {revenue:N0}</strong></p>"
                 + $"<p style=\"margin-bottom:4px\"><strong>Top products</strong></p>"
                 + $"<table style=\"border-collapse:collapse\"><tr><th style=\"text-align:left;padding:4px 8px\">Product</th><th style=\"padding:4px 8px\">Units</th></tr>{topRows}</table>";
        await email.SendAsync(recipient!, $"{storeName} sales digest — {count} order(s) {period}", body, ct);

        await UpsertSettingAsync(installationId, tenantId, "lastSentAt", now.ToString("o"), ct);
        log.LogInformation("Sales digest sent for tenant {Tenant}: {Count} orders.", tenantId, count);
        return (true, count);
    }

    private async Task<string?> AdminEmailAsync(long tenantId, CancellationToken ct) =>
        await (from u in db.Users.IgnoreQueryFilters()
               join ur in db.UserRoles on u.UserId equals ur.UserId
               join r in db.Roles on ur.RoleId equals r.RoleId
               where u.TenantId == tenantId && !u.IsDeleted && r.NormalizedName == "ADMIN" && u.Email != null
               select u.Email).FirstOrDefaultAsync(ct);

    private async Task UpsertSettingAsync(long installationId, long tenantId, string key, string value, CancellationToken ct)
    {
        var row = await db.AppSettings.FirstOrDefaultAsync(x => x.AppInstallationId == installationId && x.Key == key, ct);
        if (row is null) db.AppSettings.Add(new Data.Entities.AppSetting { TenantId = tenantId, AppInstallationId = installationId, Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        else { row.Value = value; row.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct);
    }
}
