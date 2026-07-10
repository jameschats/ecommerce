using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Storefront;

public sealed record StorefrontPreferencesDto(
    string? SeoTitle, string? SeoDescription, string? SeoImage,
    bool PasswordEnabled, string? Password, string? PasswordMessage);

public sealed record UpdateStorefrontPreferencesRequest(
    string? SeoTitle, string? SeoDescription, string? SeoImage,
    bool PasswordEnabled, string? Password, string? PasswordMessage);

public sealed record StoreSeoDto(string? Title, string? Description, string? Image);
public sealed record StoreGateDto(bool PasswordProtected, string? Message);

public interface IStorefrontPreferencesService
{
    Task<StorefrontPreferencesDto> GetAsync(CancellationToken ct = default);
    Task<StorefrontPreferencesDto> UpdateAsync(UpdateStorefrontPreferencesRequest req, CancellationToken ct = default);
    Task<StoreSeoDto> GetSeoAsync(CancellationToken ct = default);
    Task<StoreGateDto> GetGateAsync(CancellationToken ct = default);
    Task<bool> CheckPasswordAsync(string? password, CancellationToken ct = default);
}

/// <summary>
/// Store-level storefront preferences: default SEO (title/description/social image) and a pre-launch
/// password gate. Stored in the generic Settings key/value table (no migration). The gate password is a
/// soft, shared pre-launch secret (not a user credential) — returned to the admin so they can share it.
/// </summary>
public sealed class StorefrontPreferencesService(EcommerceDbContext db) : IStorefrontPreferencesService
{
    private long Tenant => db.CurrentTenantId;
    private static readonly string[] Keys =
    {
        "SeoTitle", "SeoDescription", "SeoImage", "PasswordEnabled", "PasswordValue", "PasswordMessage",
    };

    public async Task<StorefrontPreferencesDto> GetAsync(CancellationToken ct = default)
    {
        var s = await MapAsync(ct);
        return new StorefrontPreferencesDto(
            s.GetValueOrDefault("SeoTitle"), s.GetValueOrDefault("SeoDescription"), s.GetValueOrDefault("SeoImage"),
            IsTrue(s.GetValueOrDefault("PasswordEnabled")), s.GetValueOrDefault("PasswordValue"), s.GetValueOrDefault("PasswordMessage"));
    }

    public async Task<StorefrontPreferencesDto> UpdateAsync(UpdateStorefrontPreferencesRequest req, CancellationToken ct = default)
    {
        await UpsertAsync("SeoTitle", req.SeoTitle?.Trim(), ct);
        await UpsertAsync("SeoDescription", req.SeoDescription?.Trim(), ct);
        await UpsertAsync("SeoImage", req.SeoImage?.Trim(), ct);
        await UpsertAsync("PasswordEnabled", req.PasswordEnabled ? "true" : "false", ct);
        if (!string.IsNullOrWhiteSpace(req.Password)) await UpsertAsync("PasswordValue", req.Password.Trim(), ct);
        await UpsertAsync("PasswordMessage", req.PasswordMessage?.Trim(), ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    public async Task<StoreSeoDto> GetSeoAsync(CancellationToken ct = default)
    {
        var s = await MapAsync(ct);
        return new StoreSeoDto(s.GetValueOrDefault("SeoTitle"), s.GetValueOrDefault("SeoDescription"), s.GetValueOrDefault("SeoImage"));
    }

    public async Task<StoreGateDto> GetGateAsync(CancellationToken ct = default)
    {
        var s = await MapAsync(ct);
        var on = IsTrue(s.GetValueOrDefault("PasswordEnabled")) && !string.IsNullOrWhiteSpace(s.GetValueOrDefault("PasswordValue"));
        return new StoreGateDto(on, on ? s.GetValueOrDefault("PasswordMessage") : null);
    }

    public async Task<bool> CheckPasswordAsync(string? password, CancellationToken ct = default)
    {
        var s = await MapAsync(ct);
        if (!IsTrue(s.GetValueOrDefault("PasswordEnabled"))) return true;   // gate off → always allowed
        var expected = s.GetValueOrDefault("PasswordValue");
        return !string.IsNullOrEmpty(expected) && string.Equals(password?.Trim(), expected, StringComparison.Ordinal);
    }

    // ---- helpers ----
    private Task<Dictionary<string, string?>> MapAsync(CancellationToken ct) =>
        db.Settings.Where(x => x.TenantId == Tenant && Keys.Contains(x.SettingKey))
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, ct);

    private async Task UpsertAsync(string key, string? value, CancellationToken ct)
    {
        var existing = await db.Settings.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.SettingKey == key, ct);
        if (existing is null)
            db.Settings.Add(new Setting { TenantId = Tenant, SettingKey = key, SettingValue = value, DataType = "string", Category = "Storefront", CreatedAt = DateTime.UtcNow });
        else
            existing.SettingValue = value;
    }

    private static bool IsTrue(string? v) => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
}
