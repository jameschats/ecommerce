namespace ecomm.api.Data.Entities;

/// <summary>
/// A tenant's brand voice for AI-generated marketing (G1). Fed into every content prompt so the
/// output reads like the store rather than a generic assistant. One row per tenant, created lazily.
/// </summary>
public class GrowthBrandKit : ITenantScoped
{
    public long GrowthBrandKitId { get; set; }
    public long TenantId { get; set; }
    public string Tone { get; set; } = "friendly";      // friendly | premium | value | playful
    public string Language { get; set; } = "English";   // English | Hindi | Tamil | Telugu | Hinglish
    public string? Audience { get; set; }
    public bool UseEmoji { get; set; } = true;
    public string? Hashtags { get; set; }
    public string? DoNotSay { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
