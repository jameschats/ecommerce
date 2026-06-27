using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Checkout;

/// <summary>
/// GST engine: resolves a rate by HSN code (falling back to a default rate),
/// and computes the CGST/SGST vs IGST split based on whether the destination
/// state matches the store's state (intra- vs inter-state supply).
/// Scoped — caches store identity + resolved rates for the request.
/// </summary>
public interface ITaxService
{
    Task<bool> IsInterStateAsync(string? destinationState, CancellationToken ct = default);
    Task<decimal> ResolveRateAsync(string? hsnCode, CancellationToken ct = default);
    Task<TaxLine> ComputeAsync(string? hsnCode, decimal taxableAmount, bool interState, CancellationToken ct = default);
}

public sealed class TaxService : ITaxService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;
    private readonly Dictionary<string, decimal> _rateCache = new();
    private string? _storeState;
    private string? _defaultRateName;

    public TaxService(EcommerceDbContext db) => _db = db;

    public async Task<bool> IsInterStateAsync(string? destinationState, CancellationToken ct = default)
    {
        await EnsureStoreAsync(ct);
        if (string.IsNullOrWhiteSpace(destinationState) || string.IsNullOrWhiteSpace(_storeState))
            return false;   // assume intra-state when unknown
        return !Normalize(destinationState).Equals(Normalize(_storeState), StringComparison.OrdinalIgnoreCase);
    }

    public async Task<decimal> ResolveRateAsync(string? hsnCode, CancellationToken ct = default)
    {
        await EnsureStoreAsync(ct);
        var key = hsnCode ?? "__default";
        if (_rateCache.TryGetValue(key, out var cached)) return cached;

        decimal rate = 0m;
        if (!string.IsNullOrWhiteSpace(hsnCode))
        {
            rate = await _db.TaxRates.Where(t => t.TenantId == Tenant && t.IsActive && t.HsnCode == hsnCode)
                .Select(t => t.TotalRate).FirstOrDefaultAsync(ct);
        }
        if (rate == 0m && !string.IsNullOrWhiteSpace(_defaultRateName))
        {
            rate = await _db.TaxRates.Where(t => t.TenantId == Tenant && t.IsActive && t.Name == _defaultRateName)
                .Select(t => t.TotalRate).FirstOrDefaultAsync(ct);
        }
        _rateCache[key] = rate;
        return rate;
    }

    public async Task<TaxLine> ComputeAsync(string? hsnCode, decimal taxableAmount, bool interState, CancellationToken ct = default)
    {
        var rate = await ResolveRateAsync(hsnCode, ct);
        var tax = Math.Round(taxableAmount * rate / 100m, 2, MidpointRounding.AwayFromZero);
        if (interState)
            return new TaxLine(rate, tax, 0m, 0m, tax, true);
        var half = Math.Round(tax / 2m, 2, MidpointRounding.AwayFromZero);
        return new TaxLine(rate, tax, half, tax - half, 0m, false);
    }

    private async Task EnsureStoreAsync(CancellationToken ct)
    {
        if (_storeState is not null || _defaultRateName is not null) return;
        var settings = await _db.Settings
            .Where(s => s.TenantId == Tenant && (s.SettingKey == "StoreState" || s.SettingKey == "DefaultTaxRateName"))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);
        _storeState = settings.GetValueOrDefault("StoreState") ?? string.Empty;
        _defaultRateName = settings.GetValueOrDefault("DefaultTaxRateName") ?? "GST 12%";
    }

    private static string Normalize(string s) => s.Trim().Replace(" ", "").ToLowerInvariant();
}
