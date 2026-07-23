namespace ecomm.api.Data.Entities;

/// <summary>
/// A marketing campaign (G2): one goal fanned out to several channels. The per-channel copy lives in
/// <see cref="GrowthContent"/> rows carrying this campaign's id — this row is just the goal, product
/// and status the merchant thinks in.
/// </summary>
public class GrowthCampaign : ITenantScoped
{
    public long GrowthCampaignId { get; set; }
    public long TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;   // new-arrival | festival | weekend-sale | restock | clearance
    public long? ProductId { get; set; }
    public string Language { get; set; } = "English";
    public string Status { get; set; } = "Draft";      // Draft | Kept
    public long? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
