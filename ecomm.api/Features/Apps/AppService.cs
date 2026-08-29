using System.Security.Cryptography;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.PublicApi;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Apps;

public sealed record AppListingDto(long Id, string Name, string Slug, string? Description, string? IconUrl,
    string? Category, IReadOnlyList<string> RequestedScopes, bool IsEmbedded, string PricingModel, bool Installed,
    decimal Price, string BillingInterval);
public sealed record AppConsentDto(string Name, string Slug, string? IconUrl, IReadOnlyList<string> Scopes, string RedirectUri, string ClientId);
public sealed record InstalledAppDto(long InstallationId, long AppId, string Name, string Slug, string? IconUrl,
    IReadOnlyList<string> GrantedScopes, bool IsEmbedded, string? EmbedUrl, DateTime InstalledAt);

public sealed record RegisterAppRequest(string Name, string? Description, string? IconUrl, string? Category,
    IReadOnlyList<string> RedirectUris, IReadOnlyList<string> RequestedScopes, bool IsEmbedded, string? EmbedUrl, string PricingModel,
    decimal Price = 0, string BillingInterval = "once", decimal RevenueSharePercent = 0);
public sealed record RegisteredAppDto(long Id, string Name, string Slug, string ClientId, string ClientSecret, IReadOnlyList<string> RequestedScopes);
public sealed record AppAdminDto(long Id, string Name, string Slug, string ClientId, string Status, bool IsFirstParty,
    string? Category, IReadOnlyList<string> RequestedScopes, decimal Price, string BillingInterval, decimal RevenueSharePercent, int Installs);

public sealed record AuthorizeResult(string RedirectUrl);
public sealed record AppTokenResult(string AccessToken, string TokenType, IReadOnlyList<string> Scopes);

public interface IAppService
{
    // Developer / super-admin
    Task<RegisteredAppDto> RegisterFirstPartyAppAsync(RegisterAppRequest req, long? ownerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<AppAdminDto>> ListAllForAdminAsync(CancellationToken ct = default);
    Task SetAppStatusAsync(long appId, string status, CancellationToken ct = default);
    // Merchant App Store
    Task<IReadOnlyList<AppListingDto>> ListStoreAsync(CancellationToken ct = default);
    Task<AppConsentDto> GetConsentAsync(string clientId, string? scope, string redirectUri, CancellationToken ct = default);
    Task<AuthorizeResult> ApproveAsync(string clientId, string? scope, string redirectUri, string? state, long tenantId, long? userId, CancellationToken ct = default);
    Task<IReadOnlyList<InstalledAppDto>> ListInstalledAsync(CancellationToken ct = default);
    Task UninstallAsync(long installationId, CancellationToken ct = default);
    /// <summary>One-click install of a first-party app for the current tenant (no external OAuth redirect).</summary>
    Task<InstalledAppDto> InstallFirstPartyAsync(string slug, long? userId, CancellationToken ct = default);
    /// <summary>The current tenant's installation of an app by slug, or null if not installed.</summary>
    Task<InstalledAppDto?> GetInstalledBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string?>> GetSettingsAsync(long installationId, CancellationToken ct = default);
    Task SaveSettingsAsync(long installationId, IReadOnlyDictionary<string, string?> settings, CancellationToken ct = default);
    // App server (OAuth token exchange, anonymous)
    Task<AppTokenResult> ExchangeCodeAsync(string clientId, string clientSecret, string code, string redirectUri, CancellationToken ct = default);
}

/// <summary>
/// App-marketplace core (S1): first-party app registration + the OAuth 2.0 install flow. Install mints a
/// per-(app,tenant) offline access token (hashed like an API key); the shared API-key auth handler resolves
/// it, so an installed app calls the same public API within its granted scopes. First-party apps are listed
/// immediately (no review); third-party submission/review is a later phase.
/// </summary>
public sealed class AppService(EcommerceDbContext db, ICurrentTenantService tenant) : IAppService
{
    public const string TokenPrefixLiteral = "apptok_";
    private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(5);

    // ---- developer / super-admin ----

    public async Task<RegisteredAppDto> RegisterFirstPartyAppAsync(RegisterAppRequest req, long? ownerUserId, CancellationToken ct = default)
    {
        var name = (req.Name ?? "").Trim();
        if (name.Length == 0) throw new AppException("App name is required.", StatusCodes.Status400BadRequest);
        var scopes = NormalizeScopes(req.RequestedScopes);
        if (scopes.Count == 0) throw new AppException("Request at least one scope.", StatusCodes.Status400BadRequest);
        var redirects = (req.RedirectUris ?? []).Select(u => u.Trim()).Where(u => u.Length > 0).ToList();
        if (redirects.Count == 0 && !req.IsEmbedded) throw new AppException("At least one redirect URI is required.", StatusCodes.Status400BadRequest);

        var slug = await UniqueSlugAsync(Common.Slug.From(name), ct);
        var clientId = "wcapp_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        var rawSecret = "wcsec_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        var app = new App
        {
            OwnerUserId = ownerUserId, Name = name, Slug = slug, ClientId = clientId,
            ClientSecretHash = ApiKeyService.Hash(rawSecret), SecretPrefix = rawSecret[..14],
            Description = Clean(req.Description, 1000), IconUrl = Clean(req.IconUrl, 500), Category = Clean(req.Category, 60),
            RedirectUris = string.Join(",", redirects), RequestedScopes = string.Join(",", scopes),
            IsEmbedded = req.IsEmbedded, EmbedUrl = Clean(req.EmbedUrl, 500),
            PricingModel = string.IsNullOrWhiteSpace(req.PricingModel) ? "free" : req.PricingModel.Trim(),
            Price = Math.Max(0, req.Price),
            BillingInterval = req.BillingInterval == "monthly" ? "monthly" : "once",
            RevenueSharePercent = Math.Clamp(req.RevenueSharePercent, 0, 100),
            Status = "listed", IsFirstParty = true, CreatedAt = DateTime.UtcNow,
        };
        db.Apps.Add(app);
        await db.SaveChangesAsync(ct);
        return new RegisteredAppDto(app.AppId, app.Name, app.Slug, clientId, rawSecret, scopes);
    }

    public async Task<IReadOnlyList<AppAdminDto>> ListAllForAdminAsync(CancellationToken ct = default)
    {
        var apps = await db.Apps.AsNoTracking().OrderByDescending(a => a.AppId).ToListAsync(ct);
        var installs = (await db.AppInstallations.IgnoreQueryFilters().Where(i => i.Status == "installed")
            .GroupBy(i => i.AppId).Select(g => new { AppId = g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.AppId, x => x.Count);
        return apps.Select(a => new AppAdminDto(a.AppId, a.Name, a.Slug, a.ClientId, a.Status, a.IsFirstParty,
            a.Category, Split(a.RequestedScopes), a.Price, a.BillingInterval, a.RevenueSharePercent, installs.GetValueOrDefault(a.AppId))).ToList();
    }

    public async Task SetAppStatusAsync(long appId, string status, CancellationToken ct = default)
    {
        var valid = new[] { "draft", "listed", "suspended" };
        if (!valid.Contains(status)) throw new AppException("Invalid status.", StatusCodes.Status400BadRequest);
        var app = await db.Apps.FirstOrDefaultAsync(a => a.AppId == appId, ct) ?? throw new AppException("App not found.", StatusCodes.Status404NotFound);
        app.Status = status;
        app.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ---- merchant App Store ----

    public async Task<IReadOnlyList<AppListingDto>> ListStoreAsync(CancellationToken ct = default)
    {
        var apps = await db.Apps.AsNoTracking().Where(a => a.Status == "listed").OrderBy(a => a.Name).ToListAsync(ct);
        var installedAppIds = await db.AppInstallations.Where(i => i.Status == "installed").Select(i => i.AppId).ToListAsync(ct);
        var set = installedAppIds.ToHashSet();
        return apps.Select(a => ToListing(a, set.Contains(a.AppId))).ToList();
    }

    public async Task<AppConsentDto> GetConsentAsync(string clientId, string? scope, string redirectUri, CancellationToken ct = default)
    {
        var app = await FindListedByClientId(clientId, ct);
        ValidateRedirect(app, redirectUri);
        var scopes = ResolveGrantScopes(app, scope);
        return new AppConsentDto(app.Name, app.Slug, app.IconUrl, scopes, redirectUri, app.ClientId);
    }

    public async Task<AuthorizeResult> ApproveAsync(string clientId, string? scope, string redirectUri, string? state, long tenantId, long? userId, CancellationToken ct = default)
    {
        var app = await FindListedByClientId(clientId, ct);
        ValidateRedirect(app, redirectUri);
        var scopes = ResolveGrantScopes(app, scope);

        var rawCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        db.AppOAuthCodes.Add(new AppOAuthCode
        {
            CodeHash = ApiKeyService.Hash(rawCode), AppId = app.AppId, TenantId = tenantId,
            Scopes = string.Join(",", scopes), UserId = userId,
            ExpiresAt = DateTime.UtcNow.Add(CodeTtl), CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        var sep = redirectUri.Contains('?') ? '&' : '?';
        var url = $"{redirectUri}{sep}code={rawCode}" + (string.IsNullOrEmpty(state) ? "" : $"&state={Uri.EscapeDataString(state)}");
        return new AuthorizeResult(url);
    }

    public async Task<IReadOnlyList<InstalledAppDto>> ListInstalledAsync(CancellationToken ct = default)
    {
        var installs = await db.AppInstallations.AsNoTracking().Where(i => i.Status == "installed").ToListAsync(ct);
        if (installs.Count == 0) return [];
        var appIds = installs.Select(i => i.AppId).ToList();
        var apps = await db.Apps.AsNoTracking().Where(a => appIds.Contains(a.AppId)).ToDictionaryAsync(a => a.AppId, ct);
        return installs.Where(i => apps.ContainsKey(i.AppId)).Select(i =>
        {
            var a = apps[i.AppId];
            return new InstalledAppDto(i.AppInstallationId, i.AppId, a.Name, a.Slug, a.IconUrl,
                Split(i.GrantedScopes), a.IsEmbedded, a.EmbedUrl, i.InstalledAt);
        }).ToList();
    }

    public async Task UninstallAsync(long installationId, CancellationToken ct = default)
    {
        var inst = await db.AppInstallations.FirstOrDefaultAsync(i => i.AppInstallationId == installationId, ct)
                   ?? throw new AppException("Installation not found.", StatusCodes.Status404NotFound);
        inst.Status = "uninstalled";
        inst.AccessTokenHash = "";   // revoke the token
        inst.UninstalledAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ---- first-party one-click install + per-install settings ----

    public async Task<InstalledAppDto> InstallFirstPartyAsync(string slug, long? userId, CancellationToken ct = default)
    {
        var app = await db.Apps.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug && a.Status == "listed" && a.IsFirstParty, ct)
                  ?? throw new AppException("App not found.", StatusCodes.Status404NotFound);

        var inst = await db.AppInstallations.FirstOrDefaultAsync(i => i.AppId == app.AppId, ct);
        if (inst is null)
        {
            inst = new AppInstallation { AppId = app.AppId, InstalledByUserId = userId, InstalledAt = DateTime.UtcNow };
            db.AppInstallations.Add(inst);   // TenantId auto-stamped
        }
        inst.GrantedScopes = app.RequestedScopes;
        inst.Status = "installed";
        inst.UninstalledAt = null;
        await db.SaveChangesAsync(ct);

        if (app.Price > 0) await CreateInstallChargeAsync(app, inst, ct);

        return new InstalledAppDto(inst.AppInstallationId, app.AppId, app.Name, app.Slug, app.IconUrl,
            Split(inst.GrantedScopes), app.IsEmbedded, app.EmbedUrl, inst.InstalledAt);
    }

    /// <summary>Record the charge for a paid app at install, split by revenue share. One-time is marked paid
    /// immediately in dev/mock; real Razorpay collection + developer payout (Route) are deferred to when
    /// those rails are live (same posture as platform P2 billing).</summary>
    private async Task CreateInstallChargeAsync(App app, AppInstallation inst, CancellationToken ct)
    {
        if (app.BillingInterval == "once" && await db.AppCharges.AnyAsync(c => c.AppInstallationId == inst.AppInstallationId && c.Status == "paid", ct))
            return;   // don't double-charge a re-install of a one-time app
        var platformFee = Math.Round(app.Price * app.RevenueSharePercent / 100m, 2);
        var charge = new AppCharge
        {
            AppId = app.AppId, AppInstallationId = inst.AppInstallationId,
            Type = app.BillingInterval == "monthly" ? "recurring" : "onetime",
            Amount = app.Price, PlatformFee = platformFee, DeveloperShare = app.Price - platformFee,
            Status = "pending", CreatedAt = DateTime.UtcNow,
        };
        if (app.BillingInterval == "once")
        {
            charge.Status = "paid"; charge.PaidAt = DateTime.UtcNow; charge.PaymentReference = $"mock-app-{inst.AppInstallationId}";
        }
        db.AppCharges.Add(charge);   // TenantId auto-stamped
        await db.SaveChangesAsync(ct);
    }

    public async Task<InstalledAppDto?> GetInstalledBySlugAsync(string slug, CancellationToken ct = default)
    {
        var app = await db.Apps.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug, ct);
        if (app is null) return null;
        var inst = await db.AppInstallations.AsNoTracking().FirstOrDefaultAsync(i => i.AppId == app.AppId && i.Status == "installed", ct);
        return inst is null ? null : new InstalledAppDto(inst.AppInstallationId, app.AppId, app.Name, app.Slug, app.IconUrl,
            Split(inst.GrantedScopes), app.IsEmbedded, app.EmbedUrl, inst.InstalledAt);
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetSettingsAsync(long installationId, CancellationToken ct = default)
    {
        await EnsureInstallationAsync(installationId, ct);
        return await db.AppSettings.AsNoTracking().Where(s => s.AppInstallationId == installationId)
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
    }

    public async Task SaveSettingsAsync(long installationId, IReadOnlyDictionary<string, string?> settings, CancellationToken ct = default)
    {
        await EnsureInstallationAsync(installationId, ct);
        var existing = await db.AppSettings.Where(s => s.AppInstallationId == installationId).ToDictionaryAsync(s => s.Key, ct);
        foreach (var (key, value) in settings)
        {
            var k = key.Trim();
            if (k.Length == 0) continue;
            if (existing.TryGetValue(k, out var row)) { row.Value = value; row.UpdatedAt = DateTime.UtcNow; }
            else db.AppSettings.Add(new AppSetting { AppInstallationId = installationId, Key = k, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureInstallationAsync(long installationId, CancellationToken ct)
    {
        if (!await db.AppInstallations.AnyAsync(i => i.AppInstallationId == installationId, ct))
            throw new AppException("Installation not found.", StatusCodes.Status404NotFound);   // tenant-filtered
    }

    // ---- app server: OAuth token exchange (anonymous) ----

    public async Task<AppTokenResult> ExchangeCodeAsync(string clientId, string clientSecret, string code, string redirectUri, CancellationToken ct = default)
    {
        var app = await db.Apps.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.ClientId == clientId, ct)
                  ?? throw new AppException("Unknown client.", StatusCodes.Status401Unauthorized);
        if (app.ClientSecretHash != ApiKeyService.Hash(clientSecret ?? ""))
            throw new AppException("Invalid client secret.", StatusCodes.Status401Unauthorized);
        ValidateRedirect(app, redirectUri);

        var codeHash = ApiKeyService.Hash(code ?? "");
        var oauth = await db.AppOAuthCodes.FirstOrDefaultAsync(c => c.CodeHash == codeHash && c.AppId == app.AppId, ct)
                    ?? throw new AppException("Invalid authorization code.", StatusCodes.Status400BadRequest);
        if (oauth.RedeemedAt is not null) throw new AppException("Authorization code already used.", StatusCodes.Status400BadRequest);
        if (oauth.ExpiresAt < DateTime.UtcNow) throw new AppException("Authorization code expired.", StatusCodes.Status400BadRequest);

        oauth.RedeemedAt = DateTime.UtcNow;

        var rawToken = TokenPrefixLiteral + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var scopes = Split(oauth.Scopes);

        // Install under the granting tenant's scope so the ITenantScoped auto-stamp files it correctly.
        using (tenant.BeginScope(oauth.TenantId))
        {
            var inst = await db.AppInstallations.IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.TenantId == oauth.TenantId && i.AppId == app.AppId, ct);
            if (inst is null)
            {
                inst = new AppInstallation { TenantId = oauth.TenantId, AppId = app.AppId, InstalledByUserId = oauth.UserId, InstalledAt = DateTime.UtcNow };
                db.AppInstallations.Add(inst);
            }
            inst.GrantedScopes = oauth.Scopes;
            inst.AccessTokenHash = ApiKeyService.Hash(rawToken);
            inst.TokenPrefix = rawToken[..14];
            inst.Status = "installed";
            inst.UninstalledAt = null;
            await db.SaveChangesAsync(ct);
        }
        return new AppTokenResult(rawToken, "bearer", scopes);
    }

    // ---- helpers ----

    private async Task<App> FindListedByClientId(string clientId, CancellationToken ct) =>
        await db.Apps.AsNoTracking().FirstOrDefaultAsync(a => a.ClientId == clientId && a.Status == "listed", ct)
        ?? throw new AppException("App not found.", StatusCodes.Status404NotFound);

    private static void ValidateRedirect(App app, string redirectUri)
    {
        if (app.IsEmbedded && string.IsNullOrWhiteSpace(app.RedirectUris)) return;   // embedded may not use redirects
        var allowed = app.RedirectUris.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!allowed.Contains(redirectUri, StringComparer.OrdinalIgnoreCase))
            throw new AppException("redirect_uri is not registered for this app.", StatusCodes.Status400BadRequest);
    }

    /// <summary>Scopes to grant = requested scopes ∩ (optional narrowing scope param), all within the app's requested set.</summary>
    private static IReadOnlyList<string> ResolveGrantScopes(App app, string? scope)
    {
        var requested = Split(app.RequestedScopes).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var asked = string.IsNullOrWhiteSpace(scope) ? requested.ToList() : NormalizeScopes(scope.Split(' ', ','));
        var granted = asked.Where(requested.Contains).ToList();
        if (granted.Count == 0) throw new AppException("No valid scopes for this app.", StatusCodes.Status400BadRequest);
        return granted;
    }

    private static List<string> NormalizeScopes(IEnumerable<string>? scopes)
    {
        var s = (scopes ?? []).Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct().ToList();
        var invalid = s.Where(x => !ApiKeyService.ValidScopes.Contains(x)).ToList();
        if (invalid.Count > 0) throw new AppException($"Unknown scope(s): {string.Join(", ", invalid)}.", StatusCodes.Status400BadRequest);
        return s;
    }

    private async Task<string> UniqueSlugAsync(string baseSlug, CancellationToken ct)
    {
        var slug = baseSlug; var n = 2;
        while (await db.Apps.AnyAsync(a => a.Slug == slug, ct)) slug = $"{baseSlug}-{n++}";
        return slug;
    }

    private static AppListingDto ToListing(App a, bool installed) =>
        new(a.AppId, a.Name, a.Slug, a.Description, a.IconUrl, a.Category, Split(a.RequestedScopes), a.IsEmbedded, a.PricingModel, installed, a.Price, a.BillingInterval);

    private static IReadOnlyList<string> Split(string csv) => csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string? Clean(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : (v.Trim().Length <= max ? v.Trim() : v.Trim()[..max]);
}
