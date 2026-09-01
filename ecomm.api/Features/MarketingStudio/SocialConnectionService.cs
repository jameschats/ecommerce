using System.Text.Json;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>A platform card for the connections page: its status + (if connected) the account label.</summary>
public sealed record SocialConnectionDto(
    string Platform, string DisplayName, string Status, string? AccountName,
    bool Configured, DateTime? ConnectedAt, DateTime? ExpiresAt);

/// <summary>Where to send the merchant's browser to authorise a platform.</summary>
public sealed record StartConnectResult(string AuthorizeUrl);

public interface ISocialConnectionService
{
    Task<IReadOnlyList<SocialConnectionDto>> ListAsync(CancellationToken ct = default);
    Task<StartConnectResult> StartAsync(string platform, CancellationToken ct = default);
    Task<SocialConnectionDto> CompleteAsync(string platform, string code, string state, CancellationToken ct = default);
    Task DisconnectAsync(string platform, CancellationToken ct = default);
}

/// <summary>
/// Manages per-tenant social OAuth connections (MS1). One config-driven OAuth2 code path serves every
/// network (endpoints + scopes from <see cref="SocialPlatforms"/>, client id/secret from
/// <see cref="SocialOptions"/>). Tokens are encrypted via IDataProtection. The <c>state</c> is a signed
/// token carrying the tenant id, so the (unauthenticated, central-host) OAuth callback can write the
/// connection under the right tenant. Provider-specific token-exchange quirks (e.g. Pinterest Basic
/// auth) are refined per provider as real app keys land; the generic form-POST covers the common case.
/// </summary>
public sealed class SocialConnectionService(
    EcommerceDbContext db, ICurrentTenantService tenant, IDataProtectionProvider dp,
    IHttpClientFactory httpFactory, IOptions<SocialOptions> options) : ISocialConnectionService
{
    private readonly SocialOptions _opt = options.Value;
    private IDataProtector TokenProtector => dp.CreateProtector("MarketingStudio.SocialToken");
    private IDataProtector StateProtector => dp.CreateProtector("MarketingStudio.SocialState");
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(20);

    public async Task<IReadOnlyList<SocialConnectionDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.SocialConnections.AsNoTracking().ToListAsync(ct);
        var byPlatform = rows.ToDictionary(r => r.Platform, StringComparer.OrdinalIgnoreCase);

        return SocialPlatforms.All.Select(p =>
        {
            var configured = IsConfigured(p.Key);
            byPlatform.TryGetValue(p.Key, out var row);
            var status = row is null
                ? (configured ? "not_connected" : "not_configured")
                : (row.ExpiresAt is { } exp && exp <= DateTime.UtcNow ? "expired" : "connected");
            return new SocialConnectionDto(p.Key, p.DisplayName, status, row?.AccountName, configured,
                row?.ConnectedAt, row?.ExpiresAt);
        }).ToList();
    }

    public Task<StartConnectResult> StartAsync(string platform, CancellationToken ct = default)
    {
        var info = Require(platform);
        var cfg = ConfigFor(platform);
        if (string.IsNullOrWhiteSpace(cfg.ClientId) || string.IsNullOrWhiteSpace(info.AuthorizeUrl))
            throw new AppException($"{info.DisplayName} isn't set up on this platform yet. Please try again later.", StatusCodes.Status409Conflict);

        // Signed state carries the tenant so the (central-host) callback can attribute the connection.
        var nonce = Guid.NewGuid().ToString("N");
        var state = StateProtector.Protect($"{tenant.CurrentTenantId}|{info.Key}|{nonce}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = cfg.ClientId,
            ["redirect_uri"] = RedirectUri(info.Key),
            ["scope"] = info.DefaultScopes,
            ["state"] = state,
        };
        var url = info.AuthorizeUrl + "?" + string.Join("&",
            query.Where(kv => !string.IsNullOrEmpty(kv.Value))
                 .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
        return Task.FromResult(new StartConnectResult(url));
    }

    public async Task<SocialConnectionDto> CompleteAsync(string platform, string code, string state, CancellationToken ct = default)
    {
        var info = Require(platform);
        var (stateTenantId, statePlatform) = UnpackState(state);
        if (!string.Equals(statePlatform, info.Key, StringComparison.OrdinalIgnoreCase))
            throw new AppException("This authorization doesn't match the platform. Please try connecting again.", StatusCodes.Status400BadRequest);

        var token = await ExchangeCodeAsync(info, code, ct);

        // Write under the tenant carried by the signed state (the callback host may not resolve a tenant).
        using (tenant.BeginScope(stateTenantId))
        {
            var row = await db.SocialConnections.FirstOrDefaultAsync(c => c.Platform == info.Key, ct);
            var now = DateTime.UtcNow;
            if (row is null)
            {
                row = new SocialConnection { Platform = info.Key, ConnectedAt = now };
                db.SocialConnections.Add(row);
            }
            else { row.UpdatedAt = now; }

            row.AccessTokenCipher = TokenProtector.Protect(token.AccessToken);
            row.RefreshTokenCipher = token.RefreshToken is null ? null : TokenProtector.Protect(token.RefreshToken);
            row.Scopes = info.DefaultScopes;
            row.ExpiresAt = token.ExpiresInSeconds is { } secs ? now.AddSeconds(secs) : null;
            row.AccountName ??= info.DisplayName + " account";
            await db.SaveChangesAsync(ct);

            return new SocialConnectionDto(info.Key, info.DisplayName, "connected", row.AccountName, true, row.ConnectedAt, row.ExpiresAt);
        }
    }

    public async Task DisconnectAsync(string platform, CancellationToken ct = default)
    {
        var info = Require(platform);
        var row = await db.SocialConnections.FirstOrDefaultAsync(c => c.Platform == info.Key, ct);
        if (row is null) return;
        db.SocialConnections.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers ----

    private sealed record OAuthToken(string AccessToken, string? RefreshToken, int? ExpiresInSeconds);

    private async Task<OAuthToken> ExchangeCodeAsync(SocialPlatformInfo info, string code, CancellationToken ct)
    {
        var cfg = ConfigFor(info.Key);
        var http = httpFactory.CreateClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri(info.Key),
            ["client_id"] = cfg.ClientId,
            ["client_secret"] = cfg.ClientSecret,
        });
        using var resp = await http.PostAsync(info.TokenUrl, form, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new AppException($"Couldn't complete the {info.DisplayName} connection. Please try again.", StatusCodes.Status502BadGateway);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var access = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            if (string.IsNullOrEmpty(access))
                throw new AppException($"{info.DisplayName} did not return an access token.", StatusCodes.Status502BadGateway);
            var refresh = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            int? expires = root.TryGetProperty("expires_in", out var ei) && ei.TryGetInt32(out var s) ? s : null;
            return new OAuthToken(access!, refresh, expires);
        }
        catch (JsonException)
        {
            throw new AppException($"Unexpected response from {info.DisplayName}. Please try again.", StatusCodes.Status502BadGateway);
        }
    }

    private (long tenantId, string platform) UnpackState(string state)
    {
        string raw;
        try { raw = StateProtector.Unprotect(state); }
        catch { throw new AppException("This authorization link is invalid or has expired. Please try again.", StatusCodes.Status400BadRequest); }

        var parts = raw.Split('|');
        if (parts.Length != 4 || !long.TryParse(parts[0], out var tid) || !long.TryParse(parts[3], out var ts))
            throw new AppException("This authorization link is invalid. Please try again.", StatusCodes.Status400BadRequest);
        if (DateTimeOffset.FromUnixTimeSeconds(ts) < DateTimeOffset.UtcNow - StateTtl)
            throw new AppException("This authorization link has expired. Please try connecting again.", StatusCodes.Status400BadRequest);
        return (tid, parts[1]);
    }

    private string RedirectUri(string platformKey) =>
        $"{_opt.RedirectBaseUrl.TrimEnd('/')}/api/marketing/connections/{platformKey}/callback";

    private SocialProviderConfig ConfigFor(string platform) =>
        _opt.Providers.TryGetValue(platform, out var c) ? c : new SocialProviderConfig();

    private bool IsConfigured(string platform)
    {
        var info = SocialPlatforms.Get(platform);
        if (info is null || string.IsNullOrWhiteSpace(info.AuthorizeUrl)) return false;   // e.g. WhatsApp (BSP, not this flow)
        var cfg = ConfigFor(platform);
        return !string.IsNullOrWhiteSpace(cfg.ClientId) && !string.IsNullOrWhiteSpace(_opt.RedirectBaseUrl);
    }

    private static SocialPlatformInfo Require(string platform) =>
        SocialPlatforms.Get(platform) ?? throw new AppException("Unknown platform.", StatusCodes.Status404NotFound);
}
