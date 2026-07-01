using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Checkout;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Settings;

public sealed record StoreSettingsDto(string TaxMode, string? StoreState, string? StoreGstin, string? StoreLegalName);
public sealed record UpdateStoreSettingsRequest(string TaxMode, string? StoreState, string? StoreGstin, string? StoreLegalName);

public interface IStoreSettingsService
{
    Task<StoreSettingsDto> GetAsync(CancellationToken ct = default);
    Task<StoreSettingsDto> UpdateAsync(UpdateStoreSettingsRequest req, CancellationToken ct = default);
}

public sealed class StoreSettingsService : IStoreSettingsService
{
    private const long Tenant = 1;
    private static readonly string[] Keys = { "TaxMode", "StoreState", "StoreGstin", "StoreLegalName" };
    private static readonly string[] Modes = { TaxMode.Exclusive, TaxMode.Inclusive, TaxMode.None };

    private readonly EcommerceDbContext _db;
    public StoreSettingsService(EcommerceDbContext db) => _db = db;

    public async Task<StoreSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var s = await _db.Settings.Where(x => x.TenantId == Tenant && Keys.Contains(x.SettingKey))
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, ct);
        return new StoreSettingsDto(
            s.GetValueOrDefault("TaxMode") ?? TaxMode.Exclusive,
            s.GetValueOrDefault("StoreState"),
            s.GetValueOrDefault("StoreGstin"),
            s.GetValueOrDefault("StoreLegalName"));
    }

    public async Task<StoreSettingsDto> UpdateAsync(UpdateStoreSettingsRequest req, CancellationToken ct = default)
    {
        var mode = Modes.FirstOrDefault(m => m.Equals(req.TaxMode, StringComparison.OrdinalIgnoreCase))
            ?? throw new AppException("Tax mode must be Exclusive, Inclusive or None.");

        await UpsertAsync("TaxMode", mode, ct);
        await UpsertAsync("StoreState", req.StoreState?.Trim(), ct);
        await UpsertAsync("StoreGstin", req.StoreGstin?.Trim(), ct);
        await UpsertAsync("StoreLegalName", req.StoreLegalName?.Trim(), ct);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    private async Task UpsertAsync(string key, string? value, CancellationToken ct)
    {
        var existing = await _db.Settings.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.SettingKey == key, ct);
        if (existing is null)
            _db.Settings.Add(new Setting { TenantId = Tenant, SettingKey = key, SettingValue = value, DataType = "string", Category = "Billing", CreatedAt = DateTime.UtcNow });
        else
            existing.SettingValue = value;
    }
}
