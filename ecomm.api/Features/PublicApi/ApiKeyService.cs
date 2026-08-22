using System.Security.Cryptography;
using System.Text;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.PublicApi;

public sealed record ApiKeyDto(long Id, string Label, string KeyPrefix, IReadOnlyList<string> Scopes, DateTime? LastUsedAt, DateTime? RevokedAt, DateTime CreatedAt);
public sealed record CreateApiKeyRequest(string Label, IReadOnlyList<string> Scopes);
/// <summary>The raw key is only ever present in THIS response — it's never recoverable again.</summary>
public sealed record CreatedApiKeyDto(long Id, string Label, string RawKey, IReadOnlyList<string> Scopes, DateTime CreatedAt);

public interface IApiKeyService
{
    Task<IReadOnlyList<ApiKeyDto>> ListAsync(CancellationToken ct = default);
    Task<CreatedApiKeyDto> CreateAsync(CreateApiKeyRequest req, long? userId, CancellationToken ct = default);
    Task RevokeAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Merchant-managed credentials for the public API (v4 Phase 6 Track A). Stored as a one-way SHA-256
/// hash, same pattern as refresh tokens (<see cref="ecomm.api.Features.Auth.Services.JwtTokenService.HashRefreshToken"/>)
/// — there's never a need to recover the raw value, only to compare it on each request.
/// </summary>
public sealed class ApiKeyService(EcommerceDbContext db) : IApiKeyService
{
    public const string KeyPrefixLiteral = "wck_live_";

    /// <summary>The full set of scopes any key can be granted — one per public resource/verb this
    /// phase actually exposes. Deliberately small: products/orders read-only, inventory read+write
    /// (the one resource where a real write integration — multi-channel stock sync — is common
    /// enough to justify it in v1). Customer data is deliberately NOT exposed here at all yet —
    /// third-party access to customer PII is its own real privacy question, flagged not silently
    /// decided, same posture as Phase 3's DPDP flag.</summary>
    public static readonly IReadOnlyList<string> ValidScopes = ["products:read", "orders:read", "inventory:read", "inventory:write"];

    public async Task<IReadOnlyList<ApiKeyDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.ApiKeys.AsNoTracking().OrderByDescending(k => k.ApiKeyId).ToListAsync(ct);
        return rows.Select(k => new ApiKeyDto(k.ApiKeyId, k.Label, k.KeyPrefix, SplitScopes(k.Scopes), k.LastUsedAt, k.RevokedAt, k.CreatedAt)).ToList();
    }

    public async Task<CreatedApiKeyDto> CreateAsync(CreateApiKeyRequest req, long? userId, CancellationToken ct = default)
    {
        var label = (req.Label ?? "").Trim();
        if (label.Length == 0) throw new AppException("A label is required.", StatusCodes.Status400BadRequest);

        var scopes = (req.Scopes ?? []).Select(s => s.Trim().ToLowerInvariant()).Distinct().ToList();
        if (scopes.Count == 0) throw new AppException("Pick at least one scope.", StatusCodes.Status400BadRequest);
        var invalid = scopes.Where(s => !ValidScopes.Contains(s)).ToList();
        if (invalid.Count > 0) throw new AppException($"Unknown scope(s): {string.Join(", ", invalid)}.", StatusCodes.Status400BadRequest);

        var raw = KeyPrefixLiteral + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var key = new ApiKey
        {
            Label = label,
            KeyHash = Hash(raw),
            KeyPrefix = raw[..Math.Min(raw.Length, KeyPrefixLiteral.Length + 6)],
            Scopes = string.Join(",", scopes),
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
        };
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync(ct);
        return new CreatedApiKeyDto(key.ApiKeyId, key.Label, raw, scopes, key.CreatedAt);
    }

    public async Task RevokeAsync(long id, CancellationToken ct = default)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.ApiKeyId == id, ct)
                   ?? throw new AppException("API key not found.", StatusCodes.Status404NotFound);
        key.RevokedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    private static IReadOnlyList<string> SplitScopes(string scopes) =>
        scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
