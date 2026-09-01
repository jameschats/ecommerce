namespace ecomm.api.Data.Entities;

/// <summary>
/// One week's proposed content plan (Marketing Studio MS2). The AI proposes a cheap OUTLINE (no
/// creatives) from the tenant's preferences + festival calendar + catalog; the merchant reviews and
/// confirms, and only then are creatives generated (sub-step 3). Marketing* cluster, no core FKs.
/// </summary>
public class MarketingPlan : ITenantScoped
{
    public long MarketingPlanId { get; set; }
    public long TenantId { get; set; }

    /// <summary>First day of the week this plan covers (local intent, midnight).</summary>
    public DateTime WeekStart { get; set; }

    /// <summary>draft | confirmed | active | done.</summary>
    public string Status { get; set; } = "draft";

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<MarketingPlanItem> Items { get; set; } = new List<MarketingPlanItem>();
}

/// <summary>
/// One proposed creative in a weekly plan — an intent, not yet a generated asset. Carries the slot
/// (when), the type, the subject (product/festival/topic), the target channels, and the brand
/// include-toggles. Generation (sub-step 3) turns an approved item into a MarketingCreative and
/// fans out one ScheduledPost per channel.
/// </summary>
public class MarketingPlanItem : ITenantScoped
{
    public long MarketingPlanItemId { get; set; }
    public long TenantId { get; set; }
    public long MarketingPlanId { get; set; }

    public DateTime ScheduledAt { get; set; }

    /// <summary>text | poster (video arrives in MS3).</summary>
    public string Type { get; set; } = "text";

    public long? ProductId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Angle { get; set; }

    /// <summary>Comma-separated platform keys this item targets (subset of the type-allowed channels).</summary>
    public string Channels { get; set; } = string.Empty;

    public bool IncludeLogo { get; set; } = true;
    public bool IncludeName { get; set; } = true;

    /// <summary>proposed | approved | removed.</summary>
    public string Status { get; set; } = "proposed";
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public MarketingPlan? Plan { get; set; }
}
