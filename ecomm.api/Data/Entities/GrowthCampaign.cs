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
    public string Status { get; set; } = "Draft";      // Draft | Scheduled | Sending | Sent | Failed

    // --- M1 sending (owned channels) ---
    /// <summary>Owned channel this campaign sends on. V1 supports "email"; SMS/WhatsApp follow their transports.</summary>
    public string Channel { get; set; } = "email";
    /// <summary>Target customer segment: all | paid | repeat | prospect | subscribers. Marketing consent is always enforced on top.</summary>
    public string? SegmentKey { get; set; }
    /// <summary>When the send should fire (UTC). Null = never scheduled. In the past on an immediate send.</summary>
    public DateTime? ScheduledAt { get; set; }
    /// <summary>When the send actually completed.</summary>
    public DateTime? SentAt { get; set; }
    public int RecipientCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }

    public long? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
