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
/// MaxPrice: inventory (real, from day one), demand (now real — aggregate recent behavioural-event
/// velocity from the AI Commerce data layer, market-based and never per-shopper), and seasonality (a
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

        // Demand signal (AI Commerce C4): weighted recent velocity from the behavioural event layer —
        // aggregate, market-based, never per-shopper. Firms price up slightly for products with strong
        // recent demand; neutral otherwise. Bounded and clamped by the product's floor/ceiling below.
        var demandSince = DateTime.UtcNow.AddDays(-7);
        var candidateIds = candidates.Select(c => c.ProductId).ToList();
        var demandRaw = await db.CustomerEvents.AsNoTracking()
            .Where(e => e.ProductId != null && candidateIds.Contains(e.ProductId!.Value) && e.CreatedAt >= demandSince
                && (e.EventType == "view" || e.EventType == "add-to-cart"))
            .GroupBy(e => new { e.ProductId, e.EventType })
            .Select(g => new { g.Key.ProductId, g.Key.EventType, Count = g.Count() })
            .ToListAsync(ct);
        var demandWeight = new Dictionary<long, double>();
        foreach (var d in demandRaw)
            if (d.ProductId is { } pid)
                demandWeight[pid] = demandWeight.GetValueOrDefault(pid) + d.Count * (d.EventType == "add-to-cart" ? 3.0 : 1.0);

        var created = 0;
        foreach (var p in candidates)
        {
            if (alreadySuggestedToday.Contains(p.ProductId)) continue;

            var inventorySignal = InventorySignal(inventoryLookup.GetValueOrDefault(p.ProductId)?.Available, inventoryLookup.GetValueOrDefault(p.ProductId)?.Reorder);
            var demandSignal = DemandSignal(demandWeight.GetValueOrDefault(p.ProductId));
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
    /// <summary>Aggregate recent demand velocity (weighted views + add-to-cart over 7 days) → a small,
    /// bounded upward price bias. Conservative fixed thresholds for v1, same posture as the inventory
    /// signal; market-based (store-wide aggregate), never keyed to an individual shopper.</summary>
    private static decimal DemandSignal(double weight) => weight switch
    {
        >= 50 => 3m,
        >= 20 => 2m,
        >= 8 => 1m,
        _ => 0m,
    };

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
        if (demand > 0) facts.Add("recent shopper demand for this product is strong");
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
