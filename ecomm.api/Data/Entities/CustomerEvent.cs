namespace ecomm.api.Data.Entities;

/// <summary>
/// A single storefront behavioural event (AI Commerce data layer). The shared foundation Personalization,
/// Trending, and Dynamic Pricing's demand signal all read from. Captured first-party and purpose-limited
/// to on-site recommendations/analytics; <see cref="SessionId"/> is an anonymous first-party visitor id so
/// events work before login. Written in batches off the storefront hot path (never a synchronous insert).
/// </summary>
public class CustomerEvent : ITenantScoped
{
    public long CustomerEventId { get; set; }
    public long TenantId { get; set; }
    /// <summary>The logged-in customer, if known at capture time; null for anonymous visitors.</summary>
    public long? UserId { get; set; }
    /// <summary>Anonymous first-party visitor id (from the storefront), so a visitor's events group before they log in.</summary>
    public string SessionId { get; set; } = string.Empty;
    /// <summary>view | search | add-to-cart | remove-from-cart | purchase</summary>
    public string EventType { get; set; } = string.Empty;
    public long? ProductId { get; set; }
    /// <summary>Optional small JSON payload (e.g. the search term, quantity) — never PII.</summary>
    public string? Metadata { get; set; }

    // --- Traffic analytics (page-view events) ---
    /// <summary>The page path for a "page" event (e.g. "/products").</summary>
    public string? Path { get; set; }
    /// <summary>Referrer host for a page view ("direct" when none), for source attribution.</summary>
    public string? Referrer { get; set; }
    /// <summary>desktop | mobile | tablet — derived from the user agent server-side.</summary>
    public string? Device { get; set; }
    /// <summary>ISO country code from Cloudflare's CF-IPCountry header (free, no GeoIP DB).</summary>
    public string? Country { get; set; }
    /// <summary>Region/state + city from Cloudflare's visitor-location headers (CF-Region / CF-IPCity), when enabled.</summary>
    public string? Region { get; set; }
    public string? City { get; set; }

    public DateTime CreatedAt { get; set; }
}
