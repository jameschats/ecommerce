using ecomm.api.Data.Context;
using ecomm.api.Features.Shipping.Shiprocket;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Checkout;

/// <summary>
/// Resolves shipping for a destination pincode: zones can mark a pincode range
/// non-serviceable or override the rate; otherwise the active flat method
/// applies, free above its threshold. When Shiprocket is configured, its live
/// courier rate overrides the flat rate (best-effort, with fallback to manual).
/// </summary>
public interface IShippingService
{
    Task<ShippingQuote> QuoteAsync(string? pincode, decimal orderSubtotal, CancellationToken ct = default);
}

public sealed class ShippingService : IShippingService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;
    private readonly ITenantShiprocketService _shiprocket;
    public ShippingService(EcommerceDbContext db, ITenantShiprocketService shiprocket)
    {
        _db = db;
        _shiprocket = shiprocket;
    }

    public async Task<ShippingQuote> QuoteAsync(string? pincode, decimal orderSubtotal, CancellationToken ct = default)
    {
        var method = await _db.ShippingMethods
            .Where(m => m.TenantId == Tenant && m.IsActive)
            .OrderBy(m => m.ShippingMethodId)
            .FirstOrDefaultAsync(ct);

        if (method is null)
            return new ShippingQuote(false, null, "Shipping", 0m, null, "Shipping is not configured.");

        // A zone covering this pincode can mark it non-serviceable or set a rate.
        ShippingZoneRow? zone = null;
        if (!string.IsNullOrWhiteSpace(pincode))
        {
            var pin = pincode.Trim();
            zone = await _db.ShippingZones
                .Where(z => z.TenantId == Tenant && z.PincodeStart != null && z.PincodeEnd != null
                            && string.Compare(z.PincodeStart, pin) <= 0 && string.Compare(z.PincodeEnd, pin) >= 0)
                .OrderBy(z => z.ShippingZoneId)
                .Select(z => new ShippingZoneRow(z.IsServiceable, z.Rate, z.ShippingMethodId))
                .FirstOrDefaultAsync(ct);
        }

        if (zone is { Serviceable: false })
            return new ShippingQuote(false, null, method.Name, 0m, null, "We don't deliver to this pincode yet.");

        var free = method.FreeShippingThreshold is { } th && orderSubtotal >= th;

        // Live courier rate (Shiprocket) overrides the flat rate when the store uses Shiprocket; best-effort.
        if (!string.IsNullOrWhiteSpace(pincode))
        {
            var live = await _shiprocket.GetCheapestRateAsync(pincode.Trim(), weightKg: 0m, cod: false, ct);
            if (live is not null)
                return new ShippingQuote(true, method.ShippingMethodId, $"{method.Name} · {live.CourierName}",
                    free ? 0m : live.Rate, live.EstimatedDays ?? method.EstimatedDays, null);
            // live == null → not serviceable via Shiprocket or lookup failed; fall through to manual rate.
        }

        var baseRate = zone?.MethodId != null ? zone.Rate : method.BaseRate;
        var charge = free ? 0m : baseRate;
        return new ShippingQuote(true, method.ShippingMethodId, method.Name, charge, method.EstimatedDays, null);
    }

    private sealed record ShippingZoneRow(bool Serviceable, decimal Rate, long? MethodId);
}
