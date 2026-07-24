using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Checkout;

public interface IQuickOrderService
{
    Task<QuickOrderConfigDto> GetConfigAsync(CancellationToken ct = default);
    Task<QuickOrderQuoteDto> QuoteAsync(QuickOrderQuoteRequest req, CancellationToken ct = default);
}

/// <summary>
/// Prices a quick-order basket server-side (design.md §7.1).
///
/// The browser computes running totals as the buyer types — it has to, since a round trip
/// per keystroke would be unusable. This service is the authority: it re-reads every price
/// from the database and ignores whatever the client believed. A client that sends a
/// tampered or merely stale price gets the correct one back, not an error.
/// </summary>
public sealed class QuickOrderService : IQuickOrderService
{
    private const long Tenant = 1;

    /// <summary>Delivery states, as listed on the reference site's order form.</summary>
    private static readonly string[] DeliveryStates =
    [
        "Andhra Pradesh", "Assam", "Bihar", "Chhattisgarh", "Delhi", "Goa", "Gujarat",
        "Haryana", "Himachal Pradesh", "Jharkhand", "Karnataka", "Kerala", "Madhya Pradesh",
        "Maharashtra", "Odisha", "Puducherry", "Punjab", "Rajasthan", "Tamil Nadu",
        "Telangana", "Uttar Pradesh", "Uttarakhand", "West Bengal",
    ];

    private readonly EcommerceDbContext _db;

    public QuickOrderService(EcommerceDbContext db) => _db = db;

    public async Task<QuickOrderConfigDto> GetConfigAsync(CancellationToken ct = default)
    {
        var settings = await LoadSettingsAsync(ct);

        var stateMins = await _db.StateMinOrderAmounts
            .Where(s => s.TenantId == Tenant && s.IsActive)
            .OrderBy(s => s.StateName)
            .Select(s => new StateMinOrderDto(s.StateName, s.MinOrderAmount))
            .ToListAsync(ct);

        return new QuickOrderConfigDto(
            Decimal(settings, "QuickOrder.MinOrderAmount"),
            Decimal(settings, "QuickOrder.PackingChargePct"),
            stateMins,
            DeliveryStates,
            Text(settings, "QuickOrder.AnnouncementText"),
            Text(settings, "QuickOrder.PriceValidUpto"));
    }

    public async Task<QuickOrderQuoteDto> QuoteAsync(QuickOrderQuoteRequest req, CancellationToken ct = default)
    {
        var warnings = new List<string>();

        // Collapse duplicates and drop non-positive quantities before touching the database.
        var wanted = (req.Lines ?? [])
            .Where(l => l.Quantity > 0)
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var lines = new List<QuickOrderQuoteLineDto>();

        if (wanted.Count > 0)
        {
            var ids = wanted.Keys.ToList();
            var products = await _db.Products
                .Where(p => p.TenantId == Tenant && !p.IsDeleted && p.IsActive
                            && p.Status == "Active" && ids.Contains(p.ProductId))
                .Select(p => new
                {
                    p.ProductId,
                    p.Sku,
                    p.Name,
                    p.Price,
                    p.CompareAtPrice,
                    InStock = p.InventoryRecords.Sum(i => i.AvailableQty) > 0,
                })
                .ToListAsync(ct);

            var found = products.ToDictionary(p => p.ProductId);

            // A product the buyer had in their basket but that is no longer purchasable is
            // dropped from the quote and named, rather than silently priced at zero.
            foreach (var id in ids.Where(id => !found.ContainsKey(id)))
            {
                warnings.Add($"An item is no longer available and was removed (product {id}).");
                wanted.Remove(id);
            }

            foreach (var p in products.OrderBy(p => p.Name))
            {
                var qty = wanted[p.ProductId];
                lines.Add(new QuickOrderQuoteLineDto(
                    p.ProductId, p.Sku, p.Name, qty,
                    p.Price, p.CompareAtPrice,
                    Round(p.Price * qty), p.InStock));

                if (!p.InStock) warnings.Add($"{p.Name} is currently out of stock.");
            }
        }

        var settings = await LoadSettingsAsync(ct);

        var subTotal = Round(lines.Sum(l => l.LineTotal));
        var netTotal = Round(lines.Sum(l => (l.CompareAtPrice ?? l.UnitPrice) * l.Quantity));
        // Never negative, even if an MRP is mis-keyed below the selling price.
        var discountTotal = Math.Max(0m, Round(netTotal - subTotal));

        var minOrder = await ResolveMinOrderAsync(req.State, settings, ct);
        var packingPct = Decimal(settings, "QuickOrder.PackingChargePct");
        var packingCharges = Round(subTotal * packingPct / 100m);

        var beforeRounding = subTotal + packingCharges;
        var roundOffEnabled = Text(settings, "QuickOrder.RoundOffEnabled") != "false";
        var overall = roundOffEnabled ? Math.Round(beforeRounding, 0, MidpointRounding.AwayFromZero) : beforeRounding;
        var roundOff = Round(overall - beforeRounding);

        if (lines.Count > 0 && subTotal < minOrder)
            warnings.Add($"Minimum order for {req.State ?? "this state"} is ₹{minOrder:N0}.");

        return new QuickOrderQuoteDto(
            lines,
            lines.Count,
            lines.Sum(l => l.Quantity),
            netTotal,
            discountTotal,
            subTotal,
            minOrder,
            packingPct,
            packingCharges,
            roundOff,
            overall,
            lines.Count > 0 && subTotal >= minOrder,
            warnings);
    }

    /// <summary>Per-state override if one exists, otherwise the global minimum.</summary>
    private async Task<decimal> ResolveMinOrderAsync(
        string? state, Dictionary<string, string?> settings, CancellationToken ct)
    {
        var fallback = Decimal(settings, "QuickOrder.MinOrderAmount");
        if (string.IsNullOrWhiteSpace(state)) return fallback;

        var match = await _db.StateMinOrderAmounts
            .Where(s => s.TenantId == Tenant && s.IsActive && s.StateName == state)
            .Select(s => (decimal?)s.MinOrderAmount)
            .FirstOrDefaultAsync(ct);

        return match ?? fallback;
    }

    private async Task<Dictionary<string, string?>> LoadSettingsAsync(CancellationToken ct)
        => await _db.Settings
            .Where(s => s.SettingKey.StartsWith("QuickOrder.") || s.SettingKey.StartsWith("Payment."))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);

    private static decimal Decimal(Dictionary<string, string?> settings, string key)
        => settings.TryGetValue(key, out var raw) && decimal.TryParse(raw, out var value) ? value : 0m;

    private static string? Text(Dictionary<string, string?> settings, string key)
        => settings.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw : null;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
