using ecomm.api.Data.Context;
using ecomm.api.Features.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Cart;

/// <summary>
/// Emails shoppers who left items in their cart. Runs cross-tenant on a schedule (registered in
/// Program.cs). Opt-in per store: only tenants with the <c>AbandonedCartRecoveryEnabled</c> setting set
/// to "true" send anything — default OFF, so no store emails its customers until the merchant turns it
/// on. Each cart is emailed at most once (<c>RecoveryEmailSentAt</c>).
/// </summary>
public interface IAbandonedCartService
{
    /// <summary>Scheduled sweep. Returns how many recovery emails were sent this run.</summary>
    Task<int> RunRecoverySweepAsync(CancellationToken ct);
}

public sealed class AbandonedCartService(
    EcommerceDbContext db, IEmailSender email, IOptions<Common.Tenancy.TenancyOptions> tenancy, ILogger<AbandonedCartService> log)
    : IAbandonedCartService
{
    private const int RecoveryAfterHours = 4;   // give the shopper time to come back on their own first
    private const int MaxAgeDays = 7;           // don't chase ancient carts
    private const int BatchCap = 300;           // per run

    public async Task<int> RunRecoverySweepAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var upper = now.AddHours(-RecoveryAfterHours);
        var lower = now.AddDays(-MaxAgeDays);

        // Opt-in stores only (default OFF).
        var enabled = await db.Settings.IgnoreQueryFilters()
            .Where(s => s.SettingKey == "AbandonedCartRecoveryEnabled" && s.SettingValue == "true")
            .Select(s => s.TenantId).Distinct().ToListAsync(ct);
        if (enabled.Count == 0) return 0;

        var candidates = await db.Carts.IgnoreQueryFilters()
            .Where(c => c.Status == "Active" && c.UserId != null && c.RecoveryEmailSentAt == null
                && (c.UpdatedAt ?? c.CreatedAt) <= upper && (c.UpdatedAt ?? c.CreatedAt) >= lower
                && enabled.Contains(c.TenantId) && c.Items.Any())
            .OrderBy(c => c.CartId)
            .Select(c => new { c.CartId, c.TenantId, UserId = c.UserId!.Value })
            .Take(BatchCap)
            .ToListAsync(ct);
        if (candidates.Count == 0) return 0;

        var userIds = candidates.Select(c => c.UserId).Distinct().ToList();
        var users = (await db.Users.IgnoreQueryFilters()
            .Where(u => userIds.Contains(u.UserId) && u.Email != null && !u.IsDeleted)
            .Select(u => new { u.UserId, u.Email, u.FullName }).ToListAsync(ct))
            .ToDictionary(x => x.UserId);

        var tenantIds = candidates.Select(c => c.TenantId).Distinct().ToList();
        var stores = (await db.Tenants.IgnoreQueryFilters()
            .Where(t => tenantIds.Contains(t.TenantId))
            .Select(t => new { t.TenantId, t.Name, t.Slug, t.CustomDomain, t.CustomDomainVerified }).ToListAsync(ct))
            .ToDictionary(x => x.TenantId);

        var baseDomain = (tenancy.Value.BaseDomain ?? "").Trim();
        var emailed = new List<long>();
        foreach (var c in candidates)
        {
            if (!users.TryGetValue(c.UserId, out var u) || string.IsNullOrWhiteSpace(u.Email)) continue;
            if (!stores.TryGetValue(c.TenantId, out var s)) continue;

            var host = s.CustomDomainVerified && !string.IsNullOrEmpty(s.CustomDomain) ? s.CustomDomain
                     : !string.IsNullOrEmpty(baseDomain) ? $"{s.Slug}.{baseDomain}" : null;
            var cartUrl = host is null ? null : $"https://{host}/cart";
            var greeting = string.IsNullOrWhiteSpace(u.FullName) ? "Hi" : $"Hi {u.FullName}";
            var subject = $"You left something in your cart at {s.Name}";
            var body = $"<p>{System.Net.WebUtility.HtmlEncode(greeting)},</p>"
                     + $"<p>You still have items waiting in your cart at <strong>{System.Net.WebUtility.HtmlEncode(s.Name)}</strong>.</p>"
                     + (cartUrl is null ? "" : $"<p><a href=\"{cartUrl}\">Complete your order</a> before they're gone.</p>");
            try
            {
                await email.SendAsync(u.Email!, subject, body, ct, fromName: s.Name);
                emailed.Add(c.CartId);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Abandoned-cart email to {Email} (cart {CartId}) failed.", u.Email, c.CartId);
            }
        }

        if (emailed.Count > 0)
            await db.Carts.IgnoreQueryFilters().Where(c => emailed.Contains(c.CartId))
                .ExecuteUpdateAsync(u => u.SetProperty(c => c.RecoveryEmailSentAt, now), ct);

        log.LogInformation("Abandoned-cart sweep: emailed {Sent}/{Candidates} carts across {Tenants} opted-in store(s).",
            emailed.Count, candidates.Count, enabled.Count);
        return emailed.Count;
    }
}
