using System.Security.Claims;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Ai;

public sealed record AiBalanceDto(bool Enabled, int Balance, DateTime? CycleResetAt, IReadOnlyList<AiPackDto> Packs);
public sealed record AiPackDto(int PackId, string Name, int Credits, decimal PriceInr);
public sealed record AiUsageDto(long Id, string Feature, int Credits, int? Tokens, string? Model, DateTime CreatedAt);

public interface IAiCreditService
{
    Task<AiBalanceDto> GetBalanceAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AiUsageDto>> GetUsageAsync(int take = 50, CancellationToken ct = default);
    Task<AiCreditPack?> GetPackAsync(int packId, CancellationToken ct = default);
    Task<int> TopUpAsync(int packId, string reference, CancellationToken ct = default);

    /// <summary>
    /// Run a metered AI action: ensure balance → run the AI call → log usage + debit the credits, all in
    /// one save. If the AI call throws, nothing is debited. Throws 402 when the tenant is out of credits.
    /// </summary>
    Task<T> MeterAsync<T>(string feature, Func<IAiService, Task<(T Result, AiCompletion Usage)>> action, CancellationToken ct = default);
}

/// <summary>
/// The credit ledger + metering wrapper (the AI-0 foundation everything else spends through). Balance is
/// the authoritative running total; <see cref="AiUsageLog"/> is the signed audit trail. The opening
/// balance is seeded lazily from the tenant's plan allowance (<c>Plan.AiCredits</c>) on first touch.
/// </summary>
public sealed class AiCreditService(
    EcommerceDbContext db, IAiService ai, IHttpContextAccessor http) : IAiCreditService
{
    private long? CurrentUserId =>
        long.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public async Task<AiBalanceDto> GetBalanceAsync(CancellationToken ct = default)
    {
        var credit = await EnsureCreditAsync(ct);
        var packs = await db.AiCreditPacks.AsNoTracking().Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.AiCreditPackId)
            .Select(p => new AiPackDto(p.AiCreditPackId, p.Name, p.Credits, p.PriceInr))
            .ToListAsync(ct);
        return new AiBalanceDto(ai.Enabled, credit.Balance, credit.CycleResetAt, packs);
    }

    public async Task<IReadOnlyList<AiUsageDto>> GetUsageAsync(int take = 50, CancellationToken ct = default) =>
        await db.AiUsageLogs.AsNoTracking().OrderByDescending(l => l.AiUsageLogId).Take(Math.Clamp(take, 1, 200))
            .Select(l => new AiUsageDto(l.AiUsageLogId, l.Feature, l.Credits, l.Tokens, l.Model, l.CreatedAt))
            .ToListAsync(ct);

    public Task<AiCreditPack?> GetPackAsync(int packId, CancellationToken ct = default) =>
        db.AiCreditPacks.FirstOrDefaultAsync(p => p.AiCreditPackId == packId && p.IsActive, ct);

    public async Task<int> TopUpAsync(int packId, string reference, CancellationToken ct = default)
    {
        var pack = await GetPackAsync(packId, ct) ?? throw new AppException("Credit pack not found.", 404);
        var credit = await EnsureCreditAsync(ct);
        credit.Balance += pack.Credits;
        credit.UpdatedAt = DateTime.UtcNow;
        db.AiUsageLogs.Add(new AiUsageLog
        {
            Feature = "topup", Credits = pack.Credits, Model = reference, UserId = CurrentUserId, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return credit.Balance;
    }

    public async Task<T> MeterAsync<T>(string feature, Func<IAiService, Task<(T Result, AiCompletion Usage)>> action, CancellationToken ct = default)
    {
        if (!ai.Enabled) throw new AppException("AI features are not enabled on this platform.", 503);
        var cost = AiCreditPricing.CostOf(feature);
        var credit = await EnsureCreditAsync(ct);
        if (credit.Balance < cost)
            throw new AppException($"You need {cost} AI credit{(cost == 1 ? "" : "s")} for this, but have {credit.Balance}. Top up to continue.", 402);

        var (result, usage) = await action(ai);   // the AI call — if it throws, nothing below runs (no debit)

        credit.Balance -= cost;
        credit.UpdatedAt = DateTime.UtcNow;
        db.AiUsageLogs.Add(new AiUsageLog
        {
            Feature = feature, Credits = -cost, Tokens = usage.TotalTokens, Model = usage.Model,
            CostMicros = ai.EstimateCostMicros(usage), UserId = CurrentUserId, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>Get-or-create the tenant's credit row, seeding the opening balance from the current
    /// subscription's plan allowance (0 if none). The seed grant is also written to the ledger.</summary>
    private async Task<TenantAiCredit> EnsureCreditAsync(CancellationToken ct)
    {
        var credit = await db.TenantAiCredits.FirstOrDefaultAsync(ct);
        if (credit is not null) return credit;

        var sub = await db.TenantSubscriptions.Include(s => s.Plan)
            .OrderByDescending(s => s.TenantSubscriptionId).FirstOrDefaultAsync(ct);
        var grant = sub?.Plan?.AiCredits ?? 0;

        credit = new TenantAiCredit
        {
            Balance = grant, CycleGrant = grant, CycleResetAt = sub?.CurrentPeriodEnd, CreatedAt = DateTime.UtcNow,
        };
        db.TenantAiCredits.Add(credit);
        if (grant > 0)
            db.AiUsageLogs.Add(new AiUsageLog { Feature = "grant", Credits = grant, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        return credit;
    }
}
