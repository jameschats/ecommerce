namespace ecomm.api.Data.Entities;

/// <summary>
/// A generated marketing asset (Marketing Studio MS2 sub-step 3) — the output of turning an approved
/// plan item into real copy (text) and, later, a poster/video. One creative is reused across the
/// channels an item targets. Marketing* cluster, no FKs into core commerce tables.
/// </summary>
public class MarketingCreative : ITenantScoped
{
    public long MarketingCreativeId { get; set; }
    public long TenantId { get; set; }
    public long MarketingPlanItemId { get; set; }

    /// <summary>text | poster | video.</summary>
    public string Type { get; set; } = "text";
    /// <summary>generated | failed.</summary>
    public string Status { get; set; } = "generated";

    /// <summary>The copy for a text creative / caption for a poster or video.</summary>
    public string? Body { get; set; }
    /// <summary>Public URL of the rendered image/video (posters/videos); null for pure text.</summary>
    public string? OutputMediaUrl { get; set; }

    /// <summary>JSON snapshot of the editable poster spec (kind/headline/price/colours/template/format
    /// etc. — see PosterStudioRequest) that produced this creative. Null for text creatives and for
    /// posters made before this existed. Presence of a value is what makes a poster re-editable from the
    /// Library instead of only viewable/duplicable as a fixed image.</summary>
    public string? Spec { get; set; }

    public long? ProductId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// One scheduled publish of a creative to a single channel (Marketing Studio MS2). The plan fans out
/// to one row per (item × channel), so per-channel approval, scheduling, publishing and history are
/// all this one table. Status: pending_approval → scheduled → published | failed | skipped.
/// </summary>
public class ScheduledPost : ITenantScoped
{
    public long ScheduledPostId { get; set; }
    public long TenantId { get; set; }
    public long MarketingPlanItemId { get; set; }
    public long MarketingCreativeId { get; set; }

    public string Platform { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }

    /// <summary>pending_approval | scheduled | published | failed | skipped.</summary>
    public string Status { get; set; } = "pending_approval";

    public string? ExternalPostId { get; set; }
    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}
