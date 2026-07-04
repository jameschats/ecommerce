using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.SuperAdmin;

public sealed record TenantSummaryDto(
    long TenantId, string Name, string? Slug, string Standing, bool IsActive, bool Suspended,
    string? PlanName, string? SubStatus, DateTime? TrialEndsAt, DateTime CreatedAt, int UserCount, int OrderCount);

public sealed record ContactDto(long UserId, string? Email, string? FullName, string? PhoneNumber, string Roles, DateTime? LastLoginAt);

public sealed record TenantDetailDto(TenantSummaryDto Summary, IReadOnlyList<ContactDto> Contacts, string? StandingReason);

public sealed record PlanRevenueRow(string Plan, int ActiveCount, decimal Mrr);
public sealed record PlatformRevenueDto(
    decimal Mrr, int TotalTenants, int Active, int Trial, int PastDue, int Suspended, int Cancelled,
    IReadOnlyList<PlanRevenueRow> ByPlan);

public interface ISuperAdminService
{
    Task<IReadOnlyList<TenantSummaryDto>> ListTenantsAsync(string? search, CancellationToken ct);
    Task<TenantDetailDto?> GetTenantAsync(long tenantId, long adminUserId, CancellationToken ct);
    Task<PlatformRevenueDto> GetRevenueAsync(CancellationToken ct);
    Task SetStandingAsync(long tenantId, string standing, string? reason, long adminUserId, CancellationToken ct);
    Task SetActiveAsync(long tenantId, bool active, long adminUserId, CancellationToken ct);
}

/// <summary>
/// Platform-owner operations across ALL tenants. Every method reads with
/// IgnoreQueryFilters() — this is the one place cross-tenant access is allowed
/// (design-v2 §6.1). Mutations are written to PlatformAccessLog.
/// </summary>
public sealed class SuperAdminService(EcommerceDbContext db) : ISuperAdminService
{
    private static readonly HashSet<string> Standings = new(StringComparer.OrdinalIgnoreCase)
        { "Good", "Trusted", "Watch", "Flagged", "Blacklisted" };

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

        await LogAsync(adminUserId, tenantId, "ViewTenant", null, ct);
        return new TenantDetailDto(summary, contacts, t.StandingReason);
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
