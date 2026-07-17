using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.SuperAdmin;

public sealed record TenantSummaryDto(
    long TenantId, string Name, string? Slug, string Standing, bool IsActive, bool Suspended,
    string? PlanName, string? SubStatus, DateTime? TrialEndsAt, DateTime CreatedAt, int UserCount, int OrderCount);

public sealed record ContactDto(long UserId, string? Email, string? FullName, string? PhoneNumber, string Roles, DateTime? LastLoginAt);

public sealed record TenantSubscriptionInfo(string? PlanName, string? Status, DateTime? TrialEndsAt, DateTime? CurrentPeriodEnd, string? RazorpaySubscriptionId);
public sealed record TenantUsageDto(int Products, int Orders, decimal Gmv, int AiCreditBalance);

public sealed record TenantDetailDto(
    TenantSummaryDto Summary, IReadOnlyList<ContactDto> Contacts, string? StandingReason,
    TenantSubscriptionInfo Subscription, TenantUsageDto Usage,
    string? CustomDomain, bool CustomDomainVerified, IReadOnlyList<AuditDto> RecentActivity);

public sealed record PlanRevenueRow(string Plan, int ActiveCount, decimal Mrr);
public sealed record PlatformRevenueDto(
    decimal Mrr, int TotalTenants, int Active, int Trial, int PastDue, int Suspended, int Cancelled,
    IReadOnlyList<PlanRevenueRow> ByPlan);

public sealed record ImpersonationResult(string AccessToken, string StoreUrl, string Mode, DateTime ExpiresAt);
public sealed record BlocklistDto(long SignupBlocklistId, string Type, string Value, string? Reason, DateTime CreatedAt);
public sealed record AuditDto(long PlatformAccessLogId, long AdminUserId, long? TenantId, string Action, string? Detail, DateTime CreatedAt);

public interface ISuperAdminService
{
    Task<IReadOnlyList<TenantSummaryDto>> ListTenantsAsync(string? search, CancellationToken ct);
    Task<TenantDetailDto?> GetTenantAsync(long tenantId, long adminUserId, CancellationToken ct);
    Task<PlatformRevenueDto> GetRevenueAsync(CancellationToken ct);
    Task SetStandingAsync(long tenantId, string standing, string? reason, long adminUserId, CancellationToken ct);
    Task SetActiveAsync(long tenantId, bool active, long adminUserId, CancellationToken ct);
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
public sealed class SuperAdminService(EcommerceDbContext db, IJwtTokenService jwt, IOptions<TenancyOptions> tenancy) : ISuperAdminService
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

        var subs = await db.TenantSubscriptions.IgnoreQueryFilters().Include(x => x.Plan).AsNoTracking().ToListAsync(ct);
        var userCounts = await db.Users.IgnoreQueryFilters().Where(u => !u.IsDeleted)
            .GroupBy(u => u.TenantId).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
        var orderCounts = await db.Orders.IgnoreQueryFilters()
            .GroupBy(o => o.TenantId).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);

        return tenants.Select(t =>
        {
            var sub = subs.Where(x => x.TenantId == t.TenantId).OrderByDescending(x => x.TenantSubscriptionId).FirstOrDefault();
            return new TenantSummaryDto(
                t.TenantId, t.Name, t.Slug, t.Standing, t.IsActive, t.SuspendedAt is not null,
                sub?.Plan?.Name, sub?.Status, t.TrialEndsAt, t.CreatedAt,
                userCounts.FirstOrDefault(u => u.Key == t.TenantId)?.C ?? 0,
                orderCounts.FirstOrDefault(o => o.Key == t.TenantId)?.C ?? 0);
        }).ToList();
    }

    public async Task<TenantDetailDto?> GetTenantAsync(long tenantId, long adminUserId, CancellationToken ct)
    {
        var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (t is null) return null;

        var summary = (await ListTenantsAsync(null, ct)).FirstOrDefault(x => x.TenantId == tenantId)
                      ?? new TenantSummaryDto(t.TenantId, t.Name, t.Slug, t.Standing, t.IsActive, t.SuspendedAt is not null, null, null, t.TrialEndsAt, t.CreatedAt, 0, 0);

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
        var subInfo = new TenantSubscriptionInfo(sub?.Plan?.Name, sub?.Status, t.TrialEndsAt, sub?.CurrentPeriodEnd, sub?.RazorpaySubscriptionId);

        var products = await db.Products.IgnoreQueryFilters().CountAsync(p => p.TenantId == tenantId && !p.IsDeleted, ct);
        var gmv = await db.Orders.IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId && SoldStatuses.Contains(o.Status) && o.PlacedAt != null)
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
        var aiBalance = await db.TenantAiCredits.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId).Select(c => (int?)c.Balance).FirstOrDefaultAsync(ct) ?? 0;
        var usage = new TenantUsageDto(products, summary.OrderCount, gmv, aiBalance);

        var recent = await GetAuditAsync(tenantId, 15, ct);   // read before logging this view

        await LogAsync(adminUserId, tenantId, "ViewTenant", null, ct);
        return new TenantDetailDto(summary, contacts, t.StandingReason, subInfo, usage,
            t.CustomDomain, t.CustomDomainVerified, recent);
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
        t.UpdatedAt = DateTime.UtcNow;
        await LogAsync(adminUserId, tenantId, active ? "Activate" : "Suspend", null, ct);
        await db.SaveChangesAsync(ct);
    }

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
