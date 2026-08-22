using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Plans;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Pricing;

public interface IPricingEngineService
{
    /// <summary>Generates suggestions for every eligible product in the CURRENT tenant. Returns how
    /// many were created. Callable directly (e.g. an admin "check now" action) or from the
    /// cross-tenant scheduled sweep below.</summary>
    Task<int> GenerateSuggestionsForCurrentTenantAsync(CancellationToken ct = default);

    /// <summary>Hangfire-invoked (v4 Phase 0) — loops every active, entitled tenant and runs
    /// generation in each one's own tenant scope. Cross-tenant by nature (pricing is per-store),
    /// same <c>tenant.BeginScope(id)</c> pattern already used elsewhere for background/cross-tenant
    /// writes (SupportService, SuperAdminService, OnboardingService).</summary>
    Task RunScheduledGenerationAsync(CancellationToken ct = default);
}

/// <summary>
/// The pricing engine itself (v4 Phase 5): deterministic C#, not an LLM and not ML — computing a
/// price is exactly the kind of task a model is unreliable at (precise arithmetic, reproducibility,
/// auditability). Three signals, summed as percentages, clamped to the product's own MinPrice/
/// MaxPrice: inventory (real, from day one), demand (stubbed at 0 — Phase 3 Track B's event capture
/// doesn't exist yet, so this is deliberately neutral rather than fabricated), and seasonality (a
/// merchant-defined <see cref="PricingSeasonRule"/> window). The model's only job is phrasing the
/// plain-language explanation shown to the merchant from those same numbers — it never computes the
/// suggested price.
/// </summary>
public sealed class PricingEngineService(
    EcommerceDbContext db, IAiCreditService credits, ICurrentTenantService tenant, IEntitlementService entitlements) : IPricingEngineService
{
    private long Tenant => db.CurrentTenantId;

    private const string ExplanationSystemPrompt =
        "You explain a price change suggestion to a store owner in one short, plain sentence. " +
        "Use ONLY the signal facts given — never invent a reason not in them. No preamble, no options, just the sentence.";

    public async Task<int> GenerateSuggestionsForCurrentTenantAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seasonRules = await db.PricingSeasonRules.AsNoTracking()
            .Where(r => r.StartDate <= today && r.EndDate >= today)
            .ToListAsync(ct);

        var candidates = await db.Products.AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive && p.Status == "Active" && !p.PriceLocked
                        && p.MinPrice != null && p.MaxPrice != null)
            .Select(p => new { p.ProductId, p.CategoryId, p.Price, p.MinPrice, p.MaxPrice })
            .ToListAsync(ct);
        if (candidates.Count == 0) return 0;

        // Frequency cap enforced at generation, not just at apply — a product already suggested
        // today never gets a second suggestion today, regardless of how many times this runs.
        var todayStart = DateTime.UtcNow.Date;
        var alreadySuggestedToday = (await db.PriceSuggestions.AsNoTracking()
            .Where(s => s.SuggestedAt >= todayStart)
            .Select(s => s.ProductId).ToListAsync(ct)).ToHashSet();

        var inventoryByProduct = await db.Inventory.AsNoTracking()
            .Where(i => candidates.Select(c => c.ProductId).Contains(i.ProductId))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Available = g.Sum(i => i.AvailableQty), Reorder = g.Sum(i => i.ReorderLevel) })
            .ToListAsync(ct);
        var inventoryLookup = inventoryByProduct.ToDictionary(i => i.ProductId);

        var created = 0;
        foreach (var p in candidates)
        {
            if (alreadySuggestedToday.Contains(p.ProductId)) continue;

            var inventorySignal = InventorySignal(inventoryLookup.GetValueOrDefault(p.ProductId)?.Available, inventoryLookup.GetValueOrDefault(p.ProductId)?.Reorder);
            const decimal demandSignal = 0m;   // neutral until Phase 3 Track B exists — see class doc comment
            var seasonalitySignal = SeasonalitySignal(seasonRules, p.CategoryId);

            var totalPercent = inventorySignal + demandSignal + seasonalitySignal;
            if (totalPercent == 0) continue;   // nothing to suggest — avoid "no change" clutter in the queue

            var raw = Math.Round(p.Price * (1 + totalPercent / 100m), 2);
            var suggested = Math.Clamp(raw, p.MinPrice!.Value, p.MaxPrice!.Value);
            if (suggested == p.Price) continue;   // clamped back to the current price — nothing to suggest

            var reason = await ExplainAsync(inventorySignal, demandSignal, seasonalitySignal, ct);

            db.PriceSuggestions.Add(new PriceSuggestion
            {
                TenantId = Tenant, ProductId = p.ProductId, OldPrice = p.Price, SuggestedPrice = suggested,
                InventorySignalPercent = inventorySignal, DemandSignalPercent = demandSignal, SeasonalitySignalPercent = seasonalitySignal,
                Reason = reason, Status = "Pending", SuggestedAt = DateTime.UtcNow,
            });
            created++;
        }

        if (created > 0) await db.SaveChangesAsync(ct);
        return created;
    }

    public async Task RunScheduledGenerationAsync(CancellationToken ct = default)
    {
        var tenantIds = await db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.SuspendedAt == null && t.OffboardedAt == null)
            .Select(t => t.TenantId).ToListAsync(ct);

        foreach (var id in tenantIds)
        {
            using (tenant.BeginScope(id))
            {
                if (!await entitlements.HasFeatureAsync("dynamic-pricing", ct)) continue;
                await GenerateSuggestionsForCurrentTenantAsync(ct);
            }
        }
    }

    /// <summary>Stock level vs. reorder level. Deliberately simple, fixed thresholds for v1 — an
    /// ML-tuned response curve is a legitimate future direction once there's real outcome data to
    /// validate against, not something to guess at now.</summary>
    private static decimal InventorySignal(int? available, int? reorder)
    {
        if (available is null || reorder is null or <= 0) return 0;
        var ratio = (decimal)available.Value / reorder.Value;
        return ratio switch
        {
            < 0.5m => 8m,    // critically low — scarcity pricing
            < 1.0m => 4m,    // below reorder level
            > 3.0m => -5m,   // well overstocked
            _ => 0m,
        };
    }

    /// <summary>The rule with the largest absolute bias wins when more than one window overlaps,
    /// rather than summing — compounding two concurrent seasonal campaigns is more likely to
    /// surprise a merchant than help them.</summary>
    private static decimal SeasonalitySignal(List<PricingSeasonRule> rules, long categoryId)
    {
        var matching = rules.Where(r => r.CategoryId is null || r.CategoryId == categoryId).ToList();
        if (matching.Count == 0) return 0;
        return matching.OrderByDescending(r => Math.Abs(r.BiasPercent)).First().BiasPercent;
    }

    private async Task<string?> ExplainAsync(decimal inventory, decimal demand, decimal seasonality, CancellationToken ct)
    {
        var facts = new List<string>();
        if (inventory > 0) facts.Add("stock is low relative to the reorder level");
        else if (inventory < 0) facts.Add("stock is well above the reorder level");
        if (seasonality != 0) facts.Add($"an active seasonal pricing rule applies a {seasonality:0.#}% bias");
        if (facts.Count == 0) return null;

        try
        {
            return await credits.MeterAsync(AiCreditPricing.PricingExplanation, async ai =>
            {
                var c = await ai.CompleteAsync(new AiPrompt(ExplanationSystemPrompt, string.Join("; ", facts), Json: false, MaxTokens: 60), ct);
                return (c.Text.Trim(), c);
            }, ct);
        }
        catch
        {
            // An explanation hiccup shouldn't block a real, computed suggestion from reaching the queue.
            return string.Join("; ", facts);
        }
    }
}
