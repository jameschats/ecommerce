using System.Text.Json;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Auth.Services;

public interface IAuthProviderService
{
    Task<List<AuthProvider>> GetAllAsync(long tenantId, CancellationToken ct = default);
    Task<AuthProvider?> GetAsync(long tenantId, string provider, CancellationToken ct = default);
    Task<AuthProvider?> UpdateAsync(long tenantId, string provider, bool isEnabled, bool allowRegistration, int displayOrder, string? clientId, CancellationToken ct = default);
    string? GetConfigValue(AuthProvider provider, string key);
}

public sealed class AuthProviderService : IAuthProviderService
{
    private readonly EcommerceDbContext _db;

    public AuthProviderService(EcommerceDbContext db) => _db = db;

    public Task<List<AuthProvider>> GetAllAsync(long tenantId, CancellationToken ct = default) =>
        _db.AuthProviders.Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(ct);

    public Task<AuthProvider?> GetAsync(long tenantId, string provider, CancellationToken ct = default) =>
        _db.AuthProviders.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Provider == provider, ct);

    public async Task<AuthProvider?> UpdateAsync(long tenantId, string provider, bool isEnabled, bool allowRegistration, int displayOrder, string? clientId, CancellationToken ct = default)
    {
        var entity = await GetAsync(tenantId, provider, ct);
        if (entity is null) return null;

        entity.IsEnabled = isEnabled;
        entity.AllowRegistration = allowRegistration;
        entity.DisplayOrder = displayOrder;
        // clientId == null => leave config untouched; "" => clear; value => store as public config
        if (clientId is not null)
            entity.ConfigJson = string.IsNullOrWhiteSpace(clientId) ? null : JsonSerializer.Serialize(new { clientId = clientId.Trim() });
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public string? GetConfigValue(AuthProvider provider, string key)
    {
        if (string.IsNullOrWhiteSpace(provider.ConfigJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(provider.ConfigJson);
            return doc.RootElement.TryGetProperty(key, out var value) ? value.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
