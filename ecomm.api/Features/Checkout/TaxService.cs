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
    Task<string> GetTaxModeAsync(CancellationToken ct = default);

    /// <summary>Compute GST for one line given the listed line amount, rate, inter-state flag and mode.</summary>
    TaxLineResult ComputeLine(decimal listedAmount, decimal rate, bool interState, string mode);
}

public sealed class TaxService : ITaxService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;
    private readonly Dictionary<string, decimal> _rateCache = new();
    private string? _storeState;
    private string? _defaultRateName;
    private string? _taxMode;

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

    public async Task<string> GetTaxModeAsync(CancellationToken ct = default)
    {
        await EnsureStoreAsync(ct);
        return _taxMode!;
    }

    public TaxLineResult ComputeLine(decimal listedAmount, decimal rate, bool interState, string mode)
    {
        if (mode == TaxMode.None) return new TaxLineResult(0m, listedAmount, 0m, 0m, 0m, 0m);

        decimal net, tax;
        if (rate <= 0m)
        {
            net = listedAmount;
            tax = 0m;
        }
        else if (mode == TaxMode.Inclusive)
        {
            // listed amount already includes GST → reverse it out
            net = Math.Round(listedAmount / (1m + rate / 100m), 2, MidpointRounding.AwayFromZero);
            tax = listedAmount - net;
        }
        else // Exclusive: GST on top
        {
            net = listedAmount;
            tax = Math.Round(listedAmount * rate / 100m, 2, MidpointRounding.AwayFromZero);
        }

        if (interState) return new TaxLineResult(rate, net, tax, 0m, 0m, tax);
        var cgst = Math.Round(tax / 2m, 2, MidpointRounding.AwayFromZero);
        return new TaxLineResult(rate, net, tax, cgst, tax - cgst, 0m);
    }

    private async Task EnsureStoreAsync(CancellationToken ct)
    {
        if (_taxMode is not null) return;
        var settings = await _db.Settings
            .Where(s => s.TenantId == Tenant && (s.SettingKey == "StoreState" || s.SettingKey == "DefaultTaxRateName" || s.SettingKey == "TaxMode"))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);
        _storeState = settings.GetValueOrDefault("StoreState") ?? string.Empty;
        _defaultRateName = settings.GetValueOrDefault("DefaultTaxRateName") ?? "GST 12%";
        _taxMode = settings.GetValueOrDefault("TaxMode") ?? TaxMode.Exclusive;
    }

    private static string Normalize(string s) => s.Trim().Replace(" ", "").ToLowerInvariant();
}
