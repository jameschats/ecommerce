namespace ecomm.api.Data.Entities;

/// <summary>
/// One tenant's installation of an <see cref="App"/> (Shopify-App-Store style). Holds the per-(app,tenant)
/// offline access token (stored one-way, like an API key) and the scopes the merchant granted at install.
/// Tenant-scoped. The app calls the public API with this token; the shared API-key auth handler resolves it.
/// </summary>
public class AppInstallation : ITenantScoped
{
    public long AppInstallationId { get; set; }
    public long TenantId { get; set; }
    public long AppId { get; set; }
    /// <summary>Scopes granted at install (a subset of the app's requested scopes).</summary>
    public string GrantedScopes { get; set; } = string.Empty;
    public string AccessTokenHash { get; set; } = string.Empty;
    public string TokenPrefix { get; set; } = string.Empty;
    public string Status { get; set; } = "installed";   // installed | uninstalled
    public long? InstalledByUserId { get; set; }
    public DateTime InstalledAt { get; set; }
    public DateTime? UninstalledAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>Short-lived, single-use OAuth authorization code minted when a merchant approves an install.</summary>
public class AppOAuthCode
{
    public long AppOAuthCodeId { get; set; }
    public string CodeHash { get; set; } = string.Empty;   // unique
    public long AppId { get; set; }
    public long TenantId { get; set; }
    public string Scopes { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
