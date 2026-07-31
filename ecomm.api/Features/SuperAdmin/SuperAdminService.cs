using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.Notifications;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.SuperAdmin;

public sealed record TenantSummaryDto(
    long TenantId, string Name, string? Slug, string Standing, bool IsActive, bool Suspended,
    string? PlanName, string? SubStatus, DateTime? TrialEndsAt, DateTime CreatedAt, int UserCount, int OrderCount,
    int HealthScore, string HealthBand);

public sealed record HealthDto(int Score, string Band, List<string> Signals, string? SuggestedStanding);

public sealed record ContactDto(long UserId, string? Email, string? FullName, string? PhoneNumber, string Roles, DateTime? LastLoginAt);

public sealed record TenantSubscriptionInfo(int? PlanId, string? PlanName, string? Status, DateTime? TrialEndsAt, DateTime? CurrentPeriodEnd, string? RazorpaySubscriptionId);
public sealed record TenantUsageDto(int Products, int Orders, decimal Gmv, int AiCreditBalance);
public sealed record PlanDto(int PlanId, string Name, string Slug, decimal MonthlyPrice, int? MaxProducts, int? MaxOrders, int? MaxStorageMb, int AiCredits, string? Features, bool IsActive, int DisplayOrder,
    string? MarketingEngineLevel, string? LiveChatLevel, string? HelpdeskLevel,
    decimal? IntroPriceInr, int? IntroMonths, DateTime? IntroEndsAt);
public sealed record AiCreditPackDto(int AiCreditPackId, string Name, int Credits, decimal PriceInr, bool IsActive, int DisplayOrder);
public sealed record PlanUpsert(string Name, string? Slug, decimal MonthlyPrice, int? MaxProducts, int? MaxOrders, int? MaxStorageMb, int AiCredits, string? Features, bool IsActive, int DisplayOrder,
    string? MarketingEngineLevel = null, string? LiveChatLevel = null, string? HelpdeskLevel = null,
    decimal? IntroPriceInr = null, int? IntroMonths = null, DateTime? IntroEndsAt = null);
public sealed record PackUpsert(string Name, int Credits, decimal PriceInr, bool IsActive, int DisplayOrder);
public sealed record NoteDto(long TenantNoteId, long AdminUserId, string Note, DateTime CreatedAt);

public sealed record FailedNotificationDto(long Id, string Channel, string Recipient, string? Subject, string? Error, DateTime CreatedAt);
public sealed record TenantDiagnosticsDto(int FailedNotifications, int OrdersNeedingAction, int LowStock, List<FailedNotificationDto> RecentFailures);

/// <summary>Can this store actually take money? Read-only oversight — the secret is never returned.</summary>
public sealed record TenantPaymentInfo(string Provider, string? RazorpayKeyId, bool HasSecret, bool IsEnabled);

/// <summary>The platform's own gateway config (merchants paying us). Source = console | env.</summary>
public sealed record PlatformPaymentDto(string Provider, string? RazorpayKeyId, bool HasSecret, string Source);
public sealed record PlatformPaymentUpsert(string Provider, string? RazorpayKeyId, string? RazorpayKeySecret);

public sealed record BillingChargeDto(long Id, long TenantId, decimal Amount, string Status, DateTime BilledAt, DateTime? PeriodStart, DateTime? PeriodEnd, string? RazorpayPaymentId);
public sealed record SubStatusRow(long TenantId, string Name, string? Slug, string? PlanName, string Status, DateTime? CurrentPeriodEnd, DateTime? GraceEndsAt);

public sealed record TenantDetailDto(
    TenantSummaryDto Summary, IReadOnlyList<ContactDto> Contacts, string? StandingReason,
    TenantSubscriptionInfo Subscription, TenantUsageDto Usage,
    string? CustomDomain, bool CustomDomainVerified, IReadOnlyList<AuditDto> RecentActivity,
    IReadOnlyList<string> Tags, IReadOnlyList<NoteDto> Notes, DateTime? OffboardedAt,
    IReadOnlyList<BillingChargeDto> Billing, HealthDto Health, TenantPaymentInfo Payment);

public sealed record PlanRevenueRow(string Plan, int ActiveCount, decimal Mrr);
public sealed record PlatformRevenueDto(
    decimal Mrr, int TotalTenants, int Active, int Trial, int PastDue, int Suspended, int Cancelled,
    IReadOnlyList<PlanRevenueRow> ByPlan);

public sealed record StoreLeaderRow(long TenantId, string Name, string? Slug, decimal Gmv, int Orders);
public sealed record PlatformGmvPoint(DateTime Date, decimal Gmv);
public sealed record PlatformAnalyticsDto(
    decimal Gmv, int Orders, decimal Aov, int ActiveStores, int NewStores, decimal CollectedRevenue,
    List<PlatformGmvPoint> Series, List<StoreLeaderRow> TopStores);

public sealed record ImpersonationResult(string AccessToken, string StoreUrl, string Mode, DateTime ExpiresAt);
public sealed record BlocklistDto(long SignupBlocklistId, string Type, string Value, string? Reason, DateTime CreatedAt);
public sealed record AuditDto(long PlatformAccessLogId, long AdminUserId, long? TenantId, string Action, string? Detail, DateTime CreatedAt);

public interface ISuperAdminService
{
    Task<IReadOnlyList<TenantSummaryDto>> ListTenantsAsync(string? search, CancellationToken ct);
    Task<TenantDetailDto?> GetTenantAsync(long tenantId, long adminUserId, CancellationToken ct);
    Task<PlatformRevenueDto> GetRevenueAsync(CancellationToken ct);
    Task<PlatformAnalyticsDto> PlatformAnalyticsAsync(DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<SubStatusRow>> SubscriptionsAsync(string? status, CancellationToken ct);
    Task<IReadOnlyList<BillingChargeDto>> RecentChargesAsync(int limit, CancellationToken ct);
    Task RecordManualPaymentAsync(long tenantId, int planId, decimal amount, string? reference, long adminUserId, CancellationToken ct);
    Task<PlatformPaymentDto> GetPlatformPaymentAsync(CancellationToken ct);
    Task<PlatformPaymentDto> SavePlatformPaymentAsync(PlatformPaymentUpsert req, long adminUserId, CancellationToken ct);
    Task<TenantDiagnosticsDto> DiagnosticsAsync(long tenantId, CancellationToken ct);
    Task ResendNotificationAsync(long historyId, long adminUserId, CancellationToken ct);
    Task SetStandingAsync(long tenantId, string standing, string? reason, long adminUserId, CancellationToken ct);
    Task SetActiveAsync(long tenantId, bool active, long adminUserId, CancellationToken ct);
    Task<IReadOnlyList<PlanDto>> ListPlansAsync(CancellationToken ct);
    Task<PlanDto> CreatePlanAsync(PlanUpsert req, long adminUserId, CancellationToken ct);
    Task<PlanDto> UpdatePlanAsync(int planId, PlanUpsert req, long adminUserId, CancellationToken ct);
    Task<IReadOnlyList<AiCreditPackDto>> ListPacksAsync(CancellationToken ct);
    Task<AiCreditPackDto> CreatePackAsync(PackUpsert req, long adminUserId, CancellationToken ct);
    Task<AiCreditPackDto> UpdatePackAsync(int packId, PackUpsert req, long adminUserId, CancellationToken ct);
    Task GrantCreditsAsync(long tenantId, int amount, string? reason, long adminUserId, CancellationToken ct);
    Task ChangePlanAsync(long tenantId, int planId, long adminUserId, CancellationToken ct);
    Task SetTrialAsync(long tenantId, DateTime? trialEndsAt, long adminUserId, CancellationToken ct);
    Task SetTagsAsync(long tenantId, string? tags, long adminUserId, CancellationToken ct);
    Task AddNoteAsync(long tenantId, string note, long adminUserId, CancellationToken ct);
    Task OffboardAsync(long tenantId, long adminUserId, CancellationToken ct);
    Task<ImpersonationResult> ImpersonateAsync(long tenantId, string mode, long adminUserId, CancellationToken ct);
    Task<IReadOnlyList<BlocklistDto>> ListBlocklistAsync(CancellationToken ct);
    Task AddBlockAsync(string type, string value, string? reason, long adminUserId, CancellationToken ct);
    Task RemoveBlockAsync(long id, long adminUserId, CancellationToken ct);
    Task<IReadOnlyList<AuditDto>> GetAuditAsync(long? tenantId, int limit, CancellationToken ct);
}

/// <summary>
/// Platform-owner operations across ALL tenants. Every method reads with
/// IgnoreQueryFilters() — this is the one place cross-tenant access is allowed
/// (design-v2 §6.1). Mutations are written to PlatformAccessLog.
/// </summary>
public sealed class SuperAdminService(EcommerceDbContext db, IJwtTokenService jwt, IOptions<TenancyOptions> tenancy, ICurrentTenantService tenant,
    IEmailSender email, ISmsSender sms,
    ecomm.api.Features.Payments.PlatformPaymentGatewayFactory gateways,
    Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dp) : ISuperAdminService
{
    private static readonly HashSet<string> Standings = new(StringComparer.OrdinalIgnoreCase)
        { "Good", "Trusted", "Watch", "Flagged", "Blacklisted" };
    private static readonly HashSet<string> BlockTypes = new(StringComparer.OrdinalIgnoreCase) { "Email", "Gstin", "Phone" };
    private static readonly string[] SoldStatuses = { "Paid", "Confirmed", "Packed", "Shipped", "Delivered" };

    public async Task<IReadOnlyList<TenantSummaryDto>> ListTenantsAsync(string? search, CancellationToken ct)
    {
        var s = (search ?? "").Trim();
        var tenants = await db.Tenants.AsNoTracking()
            .Where(t => s == "" || t.Name.Contains(s) || (t.Slug != null && t.Slug.Contains(s)))
            .OrderBy(t => t.TenantId)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var subs = await db.TenantSubscriptions.IgnoreQueryFilters().Include(x => x.Plan).AsNoTracking().ToListAsync(ct);
        var userCounts = await db.Users.IgnoreQueryFilters().Where(u => !u.IsDeleted)
            .GroupBy(u => u.TenantId).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
        var orderHealth = await db.Orders.IgnoreQueryFilters()
            .GroupBy(o => o.TenantId)
            .Select(g => new
            {
                g.Key,
                Total = g.Count(),
                Sold = g.Count(o => SoldStatuses.Contains(o.Status)),
                Lost = g.Count(o => o.Status == "Cancelled" || o.Status == "Returned"),
                LastOrder = g.Max(o => (DateTime?)o.PlacedAt),
            }).ToListAsync(ct);
        var lastLogins = await db.Users.IgnoreQueryFilters().Where(u => !u.IsDeleted)
            .GroupBy(u => u.TenantId).Select(g => new { g.Key, Last = g.Max(u => u.LastLoginAt) }).ToListAsync(ct);

        return tenants.Select(t =>
        {
            var sub = subs.Where(x => x.TenantId == t.TenantId).OrderByDescending(x => x.TenantSubscriptionId).FirstOrDefault();
            var oh = orderHealth.FirstOrDefault(x => x.Key == t.TenantId);
            var lastLogin = lastLogins.FirstOrDefault(x => x.Key == t.TenantId)?.Last;
            var (score, band, _, _) = ComputeHealth(sub?.Status, oh?.Sold ?? 0, oh?.Lost ?? 0, oh?.LastOrder, lastLogin, t.CreatedAt, t.Standing, now);
            return new TenantSummaryDto(
                t.TenantId, t.Name, t.Slug, t.Standing, t.IsActive, t.SuspendedAt is not null,
                sub?.Plan?.Name, sub?.Status, t.TrialEndsAt, t.CreatedAt,
                userCounts.FirstOrDefault(u => u.Key == t.TenantId)?.C ?? 0,
                oh?.Total ?? 0, score, band);
        }).ToList();
    }

    // Merchant health from signals we already store: payment status, refund/cancel rate,
    // sales recency, and owner login recency. Suggests a standing escalation for the admin to confirm.
    private static (int score, string band, List<string> signals, string? suggested) ComputeHealth(
        string? subStatus, int sold, int lost, DateTime? lastOrder, DateTime? lastLogin, DateTime createdAt, string currentStanding, DateTime now)
    {
        var signals = new List<string>();
        var score = 100;

        if (subStatus == "Suspended") { score -= 50; signals.Add("Subscription suspended for non-payment"); }
        else if (subStatus == "PastDue") { score -= 30; signals.Add("Subscription past due"); }
        else if (subStatus == "Cancelled") { score -= 20; signals.Add("Subscription cancelled"); }

        var resolved = sold + lost;
        if (resolved >= 5)
        {
            var rate = (double)lost / resolved;
            if (rate > 0.30) { score -= 20; signals.Add($"High refund/cancel rate ({rate:P0})"); }
            else if (rate > 0.15) { score -= 10; signals.Add($"Elevated refund/cancel rate ({rate:P0})"); }
        }

        if (sold == 0 && (now - createdAt).TotalDays > 30) { score -= 15; signals.Add("No sales in 30+ days since signup"); }
        else if (lastOrder is not null && (now - lastOrder.Value).TotalDays > 30) { score -= 10; signals.Add($"No orders in {(int)(now - lastOrder.Value).TotalDays} days"); }

        if (lastLogin is null) { score -= 10; signals.Add("Owner has never signed in"); }
        else
        {
            var d = (int)(now - lastLogin.Value).TotalDays;
            if (d > 90) { score -= 20; signals.Add($"Owner last seen {d} days ago"); }
            else if (d > 30) { score -= 10; signals.Add($"Owner last seen {d} days ago"); }
        }

        score = Math.Clamp(score, 0, 100);
        var band = score >= 70 ? "Healthy" : score >= 40 ? "At-risk" : "Critical";

        // Only suggest an escalation, and only when the admin hasn't already set a governance standing.
        string? suggested = null;
        var managed = currentStanding is "Watch" or "Flagged" or "Blacklisted";
        if (!managed) suggested = band == "Critical" ? "Flagged" : band == "At-risk" ? "Watch" : null;
        return (score, band, signals, suggested);
    }

    public async Task<TenantDetailDto?> GetTenantAsync(long tenantId, long adminUserId, CancellationToken ct)
    {
        var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (t is null) return null;

        var summary = (await ListTenantsAsync(null, ct)).FirstOrDefault(x => x.TenantId == tenantId)
                      ?? new TenantSummaryDto(t.TenantId, t.Name, t.Slug, t.Standing, t.IsActive, t.SuspendedAt is not null, null, null, t.TrialEndsAt, t.CreatedAt, 0, 0, 100, "Healthy");

        var users = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted).ToListAsync(ct);
        var roleMap = await (from ur in db.UserRoles
                             join r in db.Roles on ur.RoleId equals r.RoleId
                             where users.Select(u => u.UserId).Contains(ur.UserId)
                             select new { ur.UserId, r.Name }).ToListAsync(ct);
        var contacts = users.Select(u => new ContactDto(
            u.UserId, u.Email, u.FullName, u.PhoneNumber,
            string.Join(", ", roleMap.Where(m => m.UserId == u.UserId).Select(m => m.Name)),
            u.LastLoginAt)).ToList();

        var sub = await db.TenantSubscriptions.IgnoreQueryFilters().Include(x => x.Plan).AsNoTracking()
            .Where(x => x.TenantId == tenantId).OrderByDescending(x => x.TenantSubscriptionId).FirstOrDefaultAsync(ct);
        var subInfo = new TenantSubscriptionInfo(sub?.PlanId, sub?.Plan?.Name, sub?.Status, t.TrialEndsAt, sub?.CurrentPeriodEnd, sub?.RazorpaySubscriptionId);

        var products = await db.Products.IgnoreQueryFilters().CountAsync(p => p.TenantId == tenantId && !p.IsDeleted, ct);
        var gmv = await db.Orders.IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId && SoldStatuses.Contains(o.Status) && o.PlacedAt != null)
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
        var aiBalance = await db.TenantAiCredits.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId).Select(c => (int?)c.Balance).FirstOrDefaultAsync(ct) ?? 0;
        var usage = new TenantUsageDto(products, summary.OrderCount, gmv, aiBalance);

        var recent = await GetAuditAsync(tenantId, 15, ct);   // read before logging this view
        var notes = await db.TenantNotes.AsNoTracking().Where(n => n.TenantId == tenantId)
            .OrderByDescending(n => n.TenantNoteId).Take(50)
            .Select(n => new NoteDto(n.TenantNoteId, n.AdminUserId, n.Note, n.CreatedAt)).ToListAsync(ct);
        var billing = await db.TenantBillingHistory.IgnoreQueryFilters().AsNoTracking().Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.TenantBillingHistoryId).Take(10)
            .Select(b => new BillingChargeDto(b.TenantBillingHistoryId, b.TenantId, b.Amount, b.Status, b.BilledAt, b.PeriodStart, b.PeriodEnd, b.RazorpayPaymentId)).ToListAsync(ct);

        var soldCount = await db.Orders.IgnoreQueryFilters().CountAsync(o => o.TenantId == tenantId && SoldStatuses.Contains(o.Status), ct);
        var lostCount = await db.Orders.IgnoreQueryFilters().CountAsync(o => o.TenantId == tenantId && (o.Status == "Cancelled" || o.Status == "Returned"), ct);
        var lastOrder = await db.Orders.IgnoreQueryFilters().Where(o => o.TenantId == tenantId).MaxAsync(o => (DateTime?)o.PlacedAt, ct);
        var lastLogin = users.Count == 0 ? (DateTime?)null : users.Max(u => u.LastLoginAt);
        var (hScore, hBand, hSignals, hSuggested) = ComputeHealth(subInfo.Status, soldCount, lostCount, lastOrder, lastLogin, t.CreatedAt, t.Standing, DateTime.UtcNow);
        var health = new HealthDto(hScore, hBand, hSignals, hSuggested);

        // Payment oversight: can this store actually take money? (Secret is never surfaced.)
        var acct = await db.TenantPaymentAccounts.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId, ct);
        var payment = new TenantPaymentInfo(acct?.Provider ?? "Mock", acct?.RazorpayKeyId,
            !string.IsNullOrEmpty(acct?.RazorpayKeySecret), acct?.IsEnabled ?? false);

        await LogAsync(adminUserId, tenantId, "ViewTenant", null, ct);
        return new TenantDetailDto(summary, contacts, t.StandingReason, subInfo, usage,
            t.CustomDomain, t.CustomDomainVerified, recent, SplitTags(t.PlatformTags), notes, t.OffboardedAt, billing, health, payment);
    }

    public async Task<IReadOnlyList<SubStatusRow>> SubscriptionsAsync(string? status, CancellationToken ct)
    {
        var subs = await db.TenantSubscriptions.IgnoreQueryFilters().Include(s => s.Plan).AsNoTracking().ToListAsync(ct);
        var latest = subs.GroupBy(s => s.TenantId).Select(g => g.OrderByDescending(x => x.TenantSubscriptionId).First()).ToList();
        if (!string.IsNullOrWhiteSpace(status)) latest = latest.Where(s => s.Status == status).ToList();
        var ids = latest.Select(s => s.TenantId).ToList();
        var tenants = await db.Tenants.Where(t => ids.Contains(t.TenantId)).Select(t => new { t.TenantId, t.Name, t.Slug }).ToListAsync(ct);
        return latest.Select(s =>
        {
            var tn = tenants.FirstOrDefault(x => x.TenantId == s.TenantId);
            return new SubStatusRow(s.TenantId, tn?.Name ?? "—", tn?.Slug, s.Plan?.Name, s.Status, s.CurrentPeriodEnd, s.GraceEndsAt);
        }).OrderBy(r => r.Status).ThenByDescending(r => r.CurrentPeriodEnd).ToList();
    }

    public async Task<IReadOnlyList<BillingChargeDto>> RecentChargesAsync(int limit, CancellationToken ct) =>
        await db.TenantBillingHistory.IgnoreQueryFilters().AsNoTracking()
            .OrderByDescending(b => b.TenantBillingHistoryId).Take(Math.Clamp(limit, 1, 500))
            .Select(b => new BillingChargeDto(b.TenantBillingHistoryId, b.TenantId, b.Amount, b.Status, b.BilledAt, b.PeriodStart, b.PeriodEnd, b.RazorpayPaymentId))
            .ToListAsync(ct);

    /// <summary>
    /// Manually record a paid month for a store (offline/bank-transfer, or comp) — the admin-override
    /// path to convert a trial to paid without the Razorpay checkout flow. Writes a "Paid" charge,
    /// activates the subscription for a 1-month period, and clears any non-payment suspension.
    /// Cross-tenant write via BeginScope so the charge lands on the target tenant.
    /// </summary>
    public async Task RecordManualPaymentAsync(long tenantId, int planId, decimal amount, string? reference, long adminUserId, CancellationToken ct)
    {
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new AppException("Tenant not found.", StatusCodes.Status404NotFound);
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.PlanId == planId, ct)
                   ?? throw new AppException("Plan not found.", StatusCodes.Status400BadRequest);
        var now = DateTime.UtcNow;
        var periodEnd = now.AddMonths(1);

        using (tenant.BeginScope(tenantId))
        {
            db.TenantBillingHistory.Add(new TenantBillingHistory
            {
                Amount = amount, Status = "Paid", RazorpayPaymentId = reference, BilledAt = now,
                PeriodStart = now, PeriodEnd = periodEnd, CreatedAt = now,
            });
            var sub = await db.TenantSubscriptions.IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId).OrderByDescending(s => s.TenantSubscriptionId).FirstOrDefaultAsync(ct);
            if (sub is null) { sub = new TenantSubscription { PlanId = planId, Status = "Active", CreatedAt = now }; db.TenantSubscriptions.Add(sub); }
            else { sub.PlanId = planId; sub.Status = "Active"; }
            sub.CurrentPeriodStart = now; sub.CurrentPeriodEnd = periodEnd; sub.GraceEndsAt = null; sub.UpdatedAt = now;

            t.PlanId = planId;
            if (t.SuspendedAt is not null) t.SuspendedAt = null;   // paying clears a non-payment suspension
            t.UpdatedAt = now;

            db.PlatformAccessLog.Add(new PlatformAccessLog { AdminUserId = adminUserId, TenantId = tenantId, Action = "RecordPayment", Detail = $"{amount} — {plan.Name}", CreatedAt = now });
            await db.SaveChangesAsync(ct);
        }
    }

    public Task<PlatformPaymentDto> GetPlatformPaymentAsync(CancellationToken ct)
    {
        var (provider, keyId, hasSecret, source) = gateways.Describe();
        return Task.FromResult(new PlatformPaymentDto(provider, keyId, hasSecret, source));
    }

    /// <summary>
    /// Store the platform's gateway config in the DB (overriding api.env). A blank secret keeps the
    /// existing one, so you can edit the key id without re-typing the secret. Never returned to the client.
    /// </summary>
    public async Task<PlatformPaymentDto> SavePlatformPaymentAsync(PlatformPaymentUpsert req, long adminUserId, CancellationToken ct)
    {
        var provider = string.Equals(req.Provider, "Razorpay", StringComparison.OrdinalIgnoreCase) ? "Razorpay" : "Mock";
        var row = await db.PlatformPaymentSettings.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new PlatformPaymentSetting { PlatformPaymentSettingId = 1, CreatedAt = DateTime.UtcNow };
            db.PlatformPaymentSettings.Add(row);
        }
        row.Provider = provider;
        row.RazorpayKeyId = req.RazorpayKeyId?.Trim();
        if (!string.IsNullOrWhiteSpace(req.RazorpayKeySecret))
            row.RazorpayKeySecret = dp.CreateProtector(ecomm.api.Features.Payments.PaymentSettingsService.ProtectorPurpose).Protect(req.RazorpayKeySecret.Trim());
        row.UpdatedAt = DateTime.UtcNow;

        if (provider == "Razorpay" && (string.IsNullOrWhiteSpace(row.RazorpayKeyId) || string.IsNullOrEmpty(row.RazorpayKeySecret)))
            throw new AppException("Razorpay needs both a key id and a key secret.", StatusCodes.Status400BadRequest);

        db.PlatformAccessLog.Add(new PlatformAccessLog { AdminUserId = adminUserId, TenantId = null, Action = "PlatformPaymentSettings", Detail = provider, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        return new PlatformPaymentDto(row.Provider, row.RazorpayKeyId, !string.IsNullOrEmpty(row.RazorpayKeySecret), "console");
    }

    public async Task<TenantDiagnosticsDto> DiagnosticsAsync(long tenantId, CancellationToken ct)
    {
        var failed = await db.NotificationHistory.IgnoreQueryFilters().CountAsync(n => n.TenantId == tenantId && n.Status == "Failed", ct);
        var needAction = await db.Orders.IgnoreQueryFilters().CountAsync(o => o.TenantId == tenantId && (o.Status == "Paid" || o.Status == "Confirmed"), ct);
        var lowStock = await db.Inventory.IgnoreQueryFilters().CountAsync(i => i.TenantId == tenantId && i.AvailableQty <= i.ReorderLevel, ct);
        var recent = await db.NotificationHistory.IgnoreQueryFilters()
            .Where(n => n.TenantId == tenantId && n.Status == "Failed")
            .OrderByDescending(n => n.NotificationHistoryId).Take(10)
            .Select(n => new FailedNotificationDto(n.NotificationHistoryId, n.Channel, n.Recipient, n.Subject, n.Error, n.CreatedAt))
            .ToListAsync(ct);
        return new TenantDiagnosticsDto(failed, needAction, lowStock, recent);
    }

    /// <summary>Re-send a previously-failed notification using its stored recipient/body. Logs a fresh history row.</summary>
    public async Task ResendNotificationAsync(long historyId, long adminUserId, CancellationToken ct)
    {
        var h = await db.NotificationHistory.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(n => n.NotificationHistoryId == historyId, ct)
                ?? throw new AppException("Notification not found.", StatusCodes.Status404NotFound);

        using (tenant.BeginScope(h.TenantId))
        {
            string status = "Sent";
            string? error = null;
            try
            {
                if (h.Channel == "Email") await email.SendAsync(h.Recipient, h.Subject ?? "", h.Body ?? "", ct);
                else await sms.SendAsync(h.Recipient, h.Body ?? "", ct);
            }
            catch (Exception ex)
            {
                status = "Failed";
                error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            }
            db.NotificationHistory.Add(new NotificationHistory
            {
                TemplateId = h.TemplateId, Channel = h.Channel, Recipient = h.Recipient, Subject = h.Subject, Body = h.Body,
                Status = status, Error = error, SentAt = status == "Sent" ? DateTime.UtcNow : null, CreatedAt = DateTime.UtcNow,
            });
            db.PlatformAccessLog.Add(new PlatformAccessLog { AdminUserId = adminUserId, TenantId = h.TenantId, Action = "ResendNotification", Detail = $"{h.Channel} → {h.Recipient}", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<PlanDto>> ListPlansAsync(CancellationToken ct) =>
        await db.Plans.AsNoTracking().OrderBy(p => p.DisplayOrder).ThenBy(p => p.PlanId)
            .Select(p => new PlanDto(p.PlanId, p.Name, p.Slug, p.MonthlyPrice, p.MaxProducts, p.MaxOrders, p.MaxStorageMb, p.AiCredits, p.Features, p.IsActive, p.DisplayOrder,
                p.MarketingEngineLevel, p.LiveChatLevel, p.HelpdeskLevel, p.IntroPriceInr, p.IntroMonths, p.IntroEndsAt))
            .ToListAsync(ct);

    public async Task<PlanDto> CreatePlanAsync(PlanUpsert r, long adminUserId, CancellationToken ct)
    {
        var name = (r.Name ?? "").Trim();
        if (name.Length == 0) throw new AppException("Plan name is required.", StatusCodes.Status400BadRequest);
        var slug = string.IsNullOrWhiteSpace(r.Slug) ? Slugify(name) : Slugify(r.Slug!);
        if (await db.Plans.AnyAsync(p => p.Slug == slug, ct)) throw new AppException("A plan with that slug already exists.", StatusCodes.Status409Conflict);
        var plan = new Plan
        {
            Name = name, Slug = slug, MonthlyPrice = r.MonthlyPrice, MaxProducts = r.MaxProducts, MaxOrders = r.MaxOrders,
            MaxStorageMb = r.MaxStorageMb, AiCredits = r.AiCredits, Features = r.Features, IsActive = r.IsActive, DisplayOrder = r.DisplayOrder,
            MarketingEngineLevel = r.MarketingEngineLevel, LiveChatLevel = r.LiveChatLevel, HelpdeskLevel = r.HelpdeskLevel,
            IntroPriceInr = r.IntroPriceInr, IntroMonths = r.IntroMonths, IntroEndsAt = r.IntroEndsAt, CreatedAt = DateTime.UtcNow,
        };
        db.Plans.Add(plan);
        await LogAsync(adminUserId, null, "CreatePlan", name, ct);
        await db.SaveChangesAsync(ct);
        return ToPlanDto(plan);
    }

    public async Task<PlanDto> UpdatePlanAsync(int planId, PlanUpsert r, long adminUserId, CancellationToken ct)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.PlanId == planId, ct)
                   ?? throw new AppException("Plan not found.", StatusCodes.Status404NotFound);
        var name = (r.Name ?? "").Trim();
        if (name.Length == 0) throw new AppException("Plan name is required.", StatusCodes.Status400BadRequest);
        var slug = string.IsNullOrWhiteSpace(r.Slug) ? Slugify(name) : Slugify(r.Slug!);
        if (await db.Plans.AnyAsync(p => p.Slug == slug && p.PlanId != planId, ct)) throw new AppException("A plan with that slug already exists.", StatusCodes.Status409Conflict);
        plan.Name = name; plan.Slug = slug; plan.MonthlyPrice = r.MonthlyPrice; plan.MaxProducts = r.MaxProducts;
        plan.MaxOrders = r.MaxOrders; plan.MaxStorageMb = r.MaxStorageMb; plan.AiCredits = r.AiCredits; plan.Features = r.Features; plan.IsActive = r.IsActive;
        plan.DisplayOrder = r.DisplayOrder;
        plan.MarketingEngineLevel = r.MarketingEngineLevel; plan.LiveChatLevel = r.LiveChatLevel; plan.HelpdeskLevel = r.HelpdeskLevel;
        plan.IntroPriceInr = r.IntroPriceInr; plan.IntroMonths = r.IntroMonths; plan.IntroEndsAt = r.IntroEndsAt;
        plan.UpdatedAt = DateTime.UtcNow;
        await LogAsync(adminUserId, null, "UpdatePlan", name, ct);
        await db.SaveChangesAsync(ct);
        return ToPlanDto(plan);
    }

    public async Task<IReadOnlyList<AiCreditPackDto>> ListPacksAsync(CancellationToken ct) =>
        await db.AiCreditPacks.AsNoTracking().OrderBy(p => p.DisplayOrder).ThenBy(p => p.AiCreditPackId)
            .Select(p => new AiCreditPackDto(p.AiCreditPackId, p.Name, p.Credits, p.PriceInr, p.IsActive, p.DisplayOrder))
            .ToListAsync(ct);

    public async Task<AiCreditPackDto> CreatePackAsync(PackUpsert r, long adminUserId, CancellationToken ct)
    {
        var name = (r.Name ?? "").Trim();
        if (name.Length == 0) throw new AppException("Pack name is required.", StatusCodes.Status400BadRequest);
        if (r.Credits <= 0) throw new AppException("Credits must be positive.", StatusCodes.Status400BadRequest);
        var pack = new AiCreditPack { Name = name, Credits = r.Credits, PriceInr = r.PriceInr, IsActive = r.IsActive, DisplayOrder = r.DisplayOrder, CreatedAt = DateTime.UtcNow };
        db.AiCreditPacks.Add(pack);
        await LogAsync(adminUserId, null, "CreatePack", name, ct);
        await db.SaveChangesAsync(ct);
        return new AiCreditPackDto(pack.AiCreditPackId, pack.Name, pack.Credits, pack.PriceInr, pack.IsActive, pack.DisplayOrder);
    }

    public async Task<AiCreditPackDto> UpdatePackAsync(int packId, PackUpsert r, long adminUserId, CancellationToken ct)
    {
        var pack = await db.AiCreditPacks.FirstOrDefaultAsync(p => p.AiCreditPackId == packId, ct)
                   ?? throw new AppException("Pack not found.", StatusCodes.Status404NotFound);
        var name = (r.Name ?? "").Trim();
        if (name.Length == 0) throw new AppException("Pack name is required.", StatusCodes.Status400BadRequest);
        if (r.Credits <= 0) throw new AppException("Credits must be positive.", StatusCodes.Status400BadRequest);
        pack.Name = name; pack.Credits = r.Credits; pack.PriceInr = r.PriceInr; pack.IsActive = r.IsActive; pack.DisplayOrder = r.DisplayOrder; pack.UpdatedAt = DateTime.UtcNow;
        await LogAsync(adminUserId, null, "UpdatePack", name, ct);
        await db.SaveChangesAsync(ct);
        return new AiCreditPackDto(pack.AiCreditPackId, pack.Name, pack.Credits, pack.PriceInr, pack.IsActive, pack.DisplayOrder);
    }

    /// <summary>Manually grant (or deduct, if negative) AI credits to a store. Cross-tenant write via BeginScope.</summary>
    public async Task GrantCreditsAsync(long tenantId, int amount, string? reason, long adminUserId, CancellationToken ct)
    {
        if (amount == 0) throw new AppException("Amount must be non-zero.", StatusCodes.Status400BadRequest);
        if (!await db.Tenants.AnyAsync(t => t.TenantId == tenantId, ct))
            throw new AppException("Tenant not found.", StatusCodes.Status404NotFound);

        using (tenant.BeginScope(tenantId))   // so the write lands in the TARGET tenant, not the super-admin's
        {
            var credit = await db.TenantAiCredits.FirstOrDefaultAsync(ct);   // scoped to tenantId now
            if (credit is null)
            {
                credit = new TenantAiCredit { Balance = 0, CreatedAt = DateTime.UtcNow };
                db.TenantAiCredits.Add(credit);
            }
            credit.Balance = Math.Max(0, credit.Balance + amount);
            credit.UpdatedAt = DateTime.UtcNow;
            db.AiUsageLogs.Add(new AiUsageLog { Feature = "grant", Credits = amount, UserId = adminUserId, Model = reason, CreatedAt = DateTime.UtcNow });
            db.PlatformAccessLog.Add(new PlatformAccessLog { AdminUserId = adminUserId, TenantId = tenantId, Action = "GrantCredits", Detail = $"{amount}: {reason}", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
        }
    }

    private static PlanDto ToPlanDto(Plan p) =>
        new(p.PlanId, p.Name, p.Slug, p.MonthlyPrice, p.MaxProducts, p.MaxOrders, p.MaxStorageMb, p.AiCredits, p.Features, p.IsActive, p.DisplayOrder,
            p.MarketingEngineLevel, p.LiveChatLevel, p.HelpdeskLevel, p.IntroPriceInr, p.IntroMonths, p.IntroEndsAt);

    private static string Slugify(string s)
    {
        var chars = s.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }

    public async Task ChangePlanAsync(long tenantId, int planId, long adminUserId, CancellationToken ct)
    {
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new AppException("Tenant not found.", StatusCodes.Status404NotFound);
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.PlanId == planId, ct)
                   ?? throw new AppException("Plan not found.", StatusCodes.Status400BadRequest);
        var sub = await db.TenantSubscriptions.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId).OrderByDescending(s => s.TenantSubscriptionId).FirstOrDefaultAsync(ct);
        if (sub is not null) { sub.PlanId = planId; sub.UpdatedAt = DateTime.UtcNow; }
        t.PlanId = planId;
        t.UpdatedAt = DateTime.UtcNow;
        await LogAsync(adminUserId, tenantId, "ChangePlan", plan.Name, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetTrialAsync(long tenantId, DateTime? trialEndsAt, long adminUserId, CancellationToken ct)
    {
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new AppException("Tenant not found.", StatusCodes.Status404NotFound);
        t.TrialEndsAt = trialEndsAt;
        t.UpdatedAt = DateTime.UtcNow;
        await LogAsync(adminUserId, tenantId, "SetTrial", trialEndsAt?.ToString("yyyy-MM-dd") ?? "cleared", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetTagsAsync(long tenantId, string? tags, long adminUserId, CancellationToken ct)
    {
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new AppException("Tenant not found.", StatusCodes.Status404NotFound);
        t.PlatformTags = NormalizeTags(tags);
        t.UpdatedAt = DateTime.UtcNow;
        await LogAsync(adminUserId, tenantId, "SetTags", t.PlatformTags, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddNoteAsync(long tenantId, string note, long adminUserId, CancellationToken ct)
    {
        note = (note ?? "").Trim();
        if (note.Length == 0) throw new AppException("Note is empty.", StatusCodes.Status400BadRequest);
        if (!await db.Tenants.AnyAsync(x => x.TenantId == tenantId, ct))
            throw new AppException("Tenant not found.", StatusCodes.Status404NotFound);
        db.TenantNotes.Add(new TenantNote { TenantId = tenantId, AdminUserId = adminUserId, Note = note, CreatedAt = DateTime.UtcNow });
        await LogAsync(adminUserId, tenantId, "AddNote", null, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task OffboardAsync(long tenantId, long adminUserId, CancellationToken ct)
    {
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new AppException("Tenant not found.", StatusCodes.Status404NotFound);
        t.OffboardedAt = DateTime.UtcNow;
        t.IsActive = false;   // TenantResolutionMiddleware then 404s the storefront
        t.UpdatedAt = DateTime.UtcNow;
        await LogAsync(adminUserId, tenantId, "Offboard", null, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PlatformRevenueDto> GetRevenueAsync(CancellationToken ct)
    {
        var subs = await db.TenantSubscriptions.IgnoreQueryFilters().Include(s => s.Plan).AsNoTracking().ToListAsync(ct);
        int Count(string st) => subs.Count(s => s.Status == st);
        var mrr = subs.Where(s => s.Status == "Active").Sum(s => s.Plan?.MonthlyPrice ?? 0);
        var byPlan = subs.Where(s => s.Status == "Active").GroupBy(s => s.Plan?.Name ?? "—")
            .Select(g => new PlanRevenueRow(g.Key, g.Count(), g.Sum(x => x.Plan?.MonthlyPrice ?? 0)))
            .OrderByDescending(r => r.Mrr).ToList();
        var total = await db.Tenants.CountAsync(ct);
        return new PlatformRevenueDto(mrr, total, Count("Active"), Count("Trial"), Count("PastDue"), Count("Suspended"), Count("Cancelled"), byPlan);
    }

    public async Task<PlatformAnalyticsDto> PlatformAnalyticsAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var sold = db.Orders.IgnoreQueryFilters().Where(o => SoldStatuses.Contains(o.Status) && o.PlacedAt >= from && o.PlacedAt <= to);

        var gmv = await sold.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
        var orders = await sold.CountAsync(ct);
        var aov = orders > 0 ? Math.Round(gmv / orders, 2) : 0m;
        var activeStores = await sold.Select(o => o.TenantId).Distinct().CountAsync(ct);
        var newStores = await db.Tenants.CountAsync(t => t.CreatedAt >= from && t.CreatedAt <= to, ct);
        var collected = await db.TenantBillingHistory.IgnoreQueryFilters()
            .Where(b => b.Status == "Paid" && b.BilledAt >= from && b.BilledAt <= to)
            .SumAsync(b => (decimal?)b.Amount, ct) ?? 0m;

        var rawSeries = await sold.GroupBy(o => o.PlacedAt!.Value.Date)
            .Select(g => new { Date = g.Key, Gmv = g.Sum(o => o.TotalAmount) }).ToListAsync(ct);
        var series = rawSeries.OrderBy(x => x.Date).Select(x => new PlatformGmvPoint(x.Date, x.Gmv)).ToList();

        var byTenant = await sold.GroupBy(o => o.TenantId)
            .Select(g => new { TenantId = g.Key, Gmv = g.Sum(o => o.TotalAmount), Orders = g.Count() }).ToListAsync(ct);
        var top = byTenant.OrderByDescending(x => x.Gmv).Take(10).ToList();
        var ids = top.Select(x => x.TenantId).ToList();
        var names = await db.Tenants.Where(t => ids.Contains(t.TenantId))
            .Select(t => new { t.TenantId, t.Name, t.Slug }).ToListAsync(ct);
        var leaderboard = top.Select(x =>
        {
            var n = names.FirstOrDefault(y => y.TenantId == x.TenantId);
            return new StoreLeaderRow(x.TenantId, n?.Name ?? "—", n?.Slug, x.Gmv, x.Orders);
        }).ToList();

        return new PlatformAnalyticsDto(gmv, orders, aov, activeStores, newStores, collected, series, leaderboard);
    }

    public async Task SetStandingAsync(long tenantId, string standing, string? reason, long adminUserId, CancellationToken ct)
    {
        if (!Standings.Contains(standing)) throw new AppException("Invalid standing.", StatusCodes.Status400BadRequest);
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new AppException("Tenant not found.", StatusCodes.Status404NotFound);
        t.Standing = standing;
        t.StandingReason = reason;
        t.StandingUpdatedAt = DateTime.UtcNow;
        t.UpdatedAt = DateTime.UtcNow;
        await LogAsync(adminUserId, tenantId, "SetStanding", $"{standing}: {reason}", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(long tenantId, bool active, long adminUserId, CancellationToken ct)
    {
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new AppException("Tenant not found.", StatusCodes.Status404NotFound);
        t.IsActive = active;
        t.SuspendedAt = active ? null : DateTime.UtcNow;
        if (active) t.OffboardedAt = null;   // reactivating clears both suspend and off-board
        t.UpdatedAt = DateTime.UtcNow;
        await LogAsync(adminUserId, tenantId, active ? "Activate" : "Suspend", null, ct);
        await db.SaveChangesAsync(ct);
    }

    private static string? NormalizeTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags) ? null
        : string.Join(",", tags.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct());

    private static IReadOnlyList<string> SplitTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags) ? []
        : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public async Task<ImpersonationResult> ImpersonateAsync(long tenantId, string mode, long adminUserId, CancellationToken ct)
    {
        mode = string.Equals(mode, "full", StringComparison.OrdinalIgnoreCase) ? "full" : "view";
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId, ct)
                     ?? throw new AppException("Tenant not found.", StatusCodes.Status404NotFound);

        var adminUser = await (
            from u in db.Users.IgnoreQueryFilters()
            join ur in db.UserRoles on u.UserId equals ur.UserId
            join r in db.Roles on ur.RoleId equals r.RoleId
            where u.TenantId == tenantId && !u.IsDeleted && r.NormalizedName == "ADMIN"
            select u).FirstOrDefaultAsync(ct)
            ?? throw new AppException("That store has no admin user to act as.", StatusCodes.Status400BadRequest);

        var roles = await db.UserRoles.Where(ur => ur.UserId == adminUser.UserId)
            .Join(db.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => r.Name).ToListAsync(ct);

        var (token, expires) = jwt.CreateImpersonationToken(adminUser, roles, mode, adminUserId);
        await LogAsync(adminUserId, tenantId, "Impersonate", $"mode={mode}, as user {adminUser.UserId}", ct);
        await db.SaveChangesAsync(ct);
        return new ImpersonationResult(token, BuildStoreUrl(tenant.Slug), mode, expires);
    }

    public async Task<IReadOnlyList<BlocklistDto>> ListBlocklistAsync(CancellationToken ct) =>
        await db.SignupBlocklist.OrderByDescending(b => b.SignupBlocklistId)
            .Select(b => new BlocklistDto(b.SignupBlocklistId, b.Type, b.Value, b.Reason, b.CreatedAt)).ToListAsync(ct);

    public async Task AddBlockAsync(string type, string value, string? reason, long adminUserId, CancellationToken ct)
    {
        if (!BlockTypes.Contains(type)) throw new AppException("Type must be Email, Gstin or Phone.", StatusCodes.Status400BadRequest);
        value = (value ?? "").Trim();
        if (type.Equals("Email", StringComparison.OrdinalIgnoreCase)) value = value.ToLowerInvariant();
        if (value.Length == 0) throw new AppException("Value is required.", StatusCodes.Status400BadRequest);
        if (await db.SignupBlocklist.AnyAsync(b => b.Type == type && b.Value == value, ct)) return;
        db.SignupBlocklist.Add(new SignupBlocklist { Type = type, Value = value, Reason = reason, CreatedByAdminId = adminUserId, CreatedAt = DateTime.UtcNow });
        await LogAsync(adminUserId, null, "AddBlock", $"{type}:{value}", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveBlockAsync(long id, long adminUserId, CancellationToken ct)
    {
        var b = await db.SignupBlocklist.FirstOrDefaultAsync(x => x.SignupBlocklistId == id, ct);
        if (b is null) return;
        db.SignupBlocklist.Remove(b);
        await LogAsync(adminUserId, null, "RemoveBlock", $"{b.Type}:{b.Value}", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditDto>> GetAuditAsync(long? tenantId, int limit, CancellationToken ct) =>
        await db.PlatformAccessLog
            .Where(a => tenantId == null || a.TenantId == tenantId)
            .OrderByDescending(a => a.PlatformAccessLogId).Take(Math.Clamp(limit, 1, 500))
            .Select(a => new AuditDto(a.PlatformAccessLogId, a.AdminUserId, a.TenantId, a.Action, a.Detail, a.CreatedAt)).ToListAsync(ct);

    private string BuildStoreUrl(string? slug)
    {
        var baseDomain = tenancy.Value.BaseDomain;
        return string.IsNullOrEmpty(baseDomain) || string.IsNullOrEmpty(slug)
            ? "/" : $"https://{slug}.{baseDomain}";
    }

    private async Task LogAsync(long adminUserId, long? tenantId, string action, string? detail, CancellationToken ct)
    {
        db.PlatformAccessLog.Add(new PlatformAccessLog
        {
            AdminUserId = adminUserId, TenantId = tenantId, Action = action, Detail = detail, CreatedAt = DateTime.UtcNow,
        });
        // saved by the caller's SaveChanges, except ViewTenant which has no other change:
        if (action == "ViewTenant") await db.SaveChangesAsync(ct);
    }
}
