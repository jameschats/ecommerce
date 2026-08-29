namespace ecomm.api.Data.Entities;

/// <summary>
/// A software-marketplace app (Shopify-App-Store style) — a developer-built integration a merchant can
/// install to extend their store. GLOBAL (an app is platform-wide; it's installed into many tenants via
/// <see cref="AppInstallation"/>). The client secret is stored one-way (shown once at creation).
/// </summary>
public class App
{
    public long AppId { get; set; }
    public long? OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;          // unique, URL-safe
    public string ClientId { get; set; } = string.Empty;       // public identifier, unique
    public string ClientSecretHash { get; set; } = string.Empty;
    public string SecretPrefix { get; set; } = string.Empty;   // first chars, for recognition
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string? Category { get; set; }
    /// <summary>Comma-separated allowed OAuth redirect URIs.</summary>
    public string RedirectUris { get; set; } = string.Empty;
    /// <summary>Comma-separated scopes the app requests (subset of the platform's ValidScopes).</summary>
    public string RequestedScopes { get; set; } = string.Empty;
    public bool IsEmbedded { get; set; }
    /// <summary>For embedded apps: the URL rendered inside the admin iframe.</summary>
    public string? EmbedUrl { get; set; }
    public string PricingModel { get; set; } = "free";         // free | onetime | recurring | usage
    /// <summary>Price charged to the merchant (GST-inclusive). 0 = free.</summary>
    public decimal Price { get; set; }
    public string BillingInterval { get; set; } = "once";      // once | monthly
    /// <summary>Platform's cut of the app fee (%). 0 for first-party (we keep it all anyway); e.g. 15 for third-party.</summary>
    public decimal RevenueSharePercent { get; set; }
    public string Status { get; set; } = "draft";              // draft | in_review | listed | suspended
    public bool IsFirstParty { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
