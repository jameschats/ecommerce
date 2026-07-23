namespace ecomm.api.Data.Entities;

/// <summary>
/// One piece of AI-generated marketing copy, saved to the content library (G1). Everything generated
/// is stored so a merchant can retrieve and reuse it rather than paying to regenerate. Never
/// auto-published — <see cref="Status"/> tracks the generate → review → keep flow only.
/// </summary>
public class GrowthContent : ITenantScoped
{
    public long GrowthContentId { get; set; }
    public long TenantId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long? ProductId { get; set; }
    /// <summary>Set when this piece was generated as part of a campaign fan-out (G2); null for a standalone generation.</summary>
    public long? CampaignId { get; set; }
    public string Language { get; set; } = "English";
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";   // Draft | Kept | Discarded
    public long? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
