namespace ecomm.api.Data.Entities;

/// <summary>
/// A tenant's VISUAL brand kit for the Marketing Studio (MS0) — logo, company name, theme colours,
/// fonts and social handles, plus the default include/exclude toggles every generated creative
/// starts from. Distinct from (and complementary to) <see cref="GrowthBrandKit"/>, which holds the
/// brand VOICE (tone/language) fed into text prompts. One row per tenant, created lazily.
///
/// Part of the Marketing Studio module (Features/MarketingStudio). Tables are clustered under the
/// <c>Marketing*</c> prefix and hold no hard FKs into core commerce tables — soft id references only —
/// so the module can later move to its own service/database (see marketing-studio-plan.md §3.10).
/// </summary>
public class MarketingBrandProfile : ITenantScoped
{
    public long MarketingBrandProfileId { get; set; }
    public long TenantId { get; set; }

    public string? CompanyName { get; set; }
    public string? Tagline { get; set; }
    public string? LogoUrl { get; set; }

    // Brand colours — hex (#rrggbb). Default-seeded from the active theme where available, else neutrals.
    public string PrimaryColor { get; set; } = "#111827";
    public string SecondaryColor { get; set; } = "#6b7280";
    public string AccentColor { get; set; } = "#2563eb";
    public string? Font { get; set; }

    // Defaults every creative's include/exclude toggles start from.
    public bool IncludeLogoByDefault { get; set; } = true;
    public bool IncludeNameByDefault { get; set; } = true;

    // Social handles / links (soft strings; used later for connections + on-creative attribution).
    public string? InstagramHandle { get; set; }
    public string? FacebookHandle { get; set; }
    public string? LinkedInHandle { get; set; }
    public string? PinterestHandle { get; set; }
    public string? YouTubeHandle { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? WebsiteUrl { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
