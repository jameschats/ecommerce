using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Settings;

// Admin view/update — checkout behaviour + customer-account self-service.
public sealed record CheckoutSettingsDto(
    string ContactMethod, bool RequirePhone, bool TippingEnabled, string? TipPresets, int ItemLimit,
    bool SelfServeCancel, bool SelfServeReturns);
public sealed record UpdateCheckoutSettingsRequest(
    string ContactMethod, bool RequirePhone, bool TippingEnabled, string? TipPresets, int ItemLimit,
    bool SelfServeCancel, bool SelfServeReturns);

// Public storefront-facing subset (what the checkout/account UI needs to render).
public sealed record PublicCheckoutSettingsDto(
    string ContactMethod, bool RequirePhone, bool TippingEnabled, IReadOnlyList<int> TipPresets, int ItemLimit,
    bool SelfServeCancel, bool SelfServeReturns);

public interface ICheckoutSettingsService
{
    Task<CheckoutSettingsDto> GetAsync(CancellationToken ct = default);
    Task<CheckoutSettingsDto> UpdateAsync(UpdateCheckoutSettingsRequest req, CancellationToken ct = default);
    Task<PublicCheckoutSettingsDto> GetPublicAsync(CancellationToken ct = default);
}

/// <summary>
/// Merchant checkout preferences (contact method, required fields, tipping, per-item quantity cap)
/// and customer-account self-service toggles. Stored in the generic Settings key/value table.
/// Enforced server-side where cheap: <c>ItemLimit</c> in the cart, <c>SelfServeCancel</c> in orders.
/// Tipping/contact-method/required-fields are exposed for the checkout UI to honour.
/// </summary>
public sealed class CheckoutSettingsService(EcommerceDbContext db) : ICheckoutSettingsService
{
    private long Tenant => db.CurrentTenantId;
    public const string ItemLimitKey = "CheckoutItemLimit";
    public const string SelfServeCancelKey = "AccountSelfServeCancel";

    private static readonly string[] Keys =
    {
        "CheckoutContactMethod", "CheckoutRequirePhone", "CheckoutTippingEnabled", "CheckoutTipPresets",
        ItemLimitKey, SelfServeCancelKey, "AccountSelfServeReturns",
    };
    private static readonly string[] ContactMethods = { "email", "phone" };

    public async Task<CheckoutSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var s = await MapAsync(ct);
        return new CheckoutSettingsDto(
            s.GetValueOrDefault("CheckoutContactMethod") ?? "email",
            IsTrue(s.GetValueOrDefault("CheckoutRequirePhone")),
            IsTrue(s.GetValueOrDefault("CheckoutTippingEnabled")),
            s.GetValueOrDefault("CheckoutTipPresets"),
            ParseInt(s.GetValueOrDefault(ItemLimitKey)),
            // Self-serve cancel defaults ON (existing behaviour); returns default OFF (no workflow yet).
            !s.ContainsKey(SelfServeCancelKey) || IsTrue(s.GetValueOrDefault(SelfServeCancelKey)),
            IsTrue(s.GetValueOrDefault("AccountSelfServeReturns")));
    }

    public async Task<CheckoutSettingsDto> UpdateAsync(UpdateCheckoutSettingsRequest req, CancellationToken ct = default)
    {
        var method = ContactMethods.FirstOrDefault(m => m.Equals(req.ContactMethod, StringComparison.OrdinalIgnoreCase))
            ?? throw new AppException("Contact method must be 'email' or 'phone'.");
        if (req.ItemLimit < 0) throw new AppException("Item limit cannot be negative.");

        await UpsertAsync("CheckoutContactMethod", method, ct);
        await UpsertAsync("CheckoutRequirePhone", Bool(req.RequirePhone), ct);
        await UpsertAsync("CheckoutTippingEnabled", Bool(req.TippingEnabled), ct);
        await UpsertAsync("CheckoutTipPresets", NormalizePresets(req.TipPresets), ct);
        await UpsertAsync(ItemLimitKey, req.ItemLimit.ToString(), ct);
        await UpsertAsync(SelfServeCancelKey, Bool(req.SelfServeCancel), ct);
        await UpsertAsync("AccountSelfServeReturns", Bool(req.SelfServeReturns), ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    public async Task<PublicCheckoutSettingsDto> GetPublicAsync(CancellationToken ct = default)
    {
        var d = await GetAsync(ct);
        return new PublicCheckoutSettingsDto(
            d.ContactMethod, d.RequirePhone, d.TippingEnabled, ParsePresets(d.TipPresets), d.ItemLimit,
            d.SelfServeCancel, d.SelfServeReturns);
    }

    // ---- helpers ----
    private Task<Dictionary<string, string?>> MapAsync(CancellationToken ct) =>
        db.Settings.Where(x => x.TenantId == Tenant && Keys.Contains(x.SettingKey))
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, ct);

    private async Task UpsertAsync(string key, string? value, CancellationToken ct)
    {
        var existing = await db.Settings.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.SettingKey == key, ct);
        if (existing is null)
            db.Settings.Add(new Setting { TenantId = Tenant, SettingKey = key, SettingValue = value, DataType = "string", Category = "Checkout", CreatedAt = DateTime.UtcNow });
        else
            existing.SettingValue = value;
    }

    private static bool IsTrue(string? v) => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    private static string Bool(bool b) => b ? "true" : "false";
    private static int ParseInt(string? v) => int.TryParse(v, out var n) && n > 0 ? n : 0;

    /// <summary>Normalise "5, 10 ,15,,x" → "5,10,15" (positive ints, deduped, ordered).</summary>
    private static string? NormalizePresets(string? raw)
    {
        var list = ParsePresets(raw);
        return list.Count == 0 ? null : string.Join(",", list);
    }

    private static List<int> ParsePresets(string? raw) =>
        (raw ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => int.TryParse(p, out var n) ? n : -1)
            .Where(n => n > 0).Distinct().OrderBy(n => n).ToList();
}
