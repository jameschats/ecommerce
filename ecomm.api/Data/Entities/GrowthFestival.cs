namespace ecomm.api.Data.Entities;

/// <summary>
/// A festival / commercial occasion for the marketing calendar (G3). GLOBAL — deliberately NOT
/// <see cref="ITenantScoped"/>: the Indian festival calendar is the same for every store, so it's
/// seeded once platform-wide (like Role/Permission). Dates are indicative — lunar-calendar festivals
/// shift year to year — and are meant to drive lead-time nudges, not transactions.
/// </summary>
public class GrowthFestival
{
    public long GrowthFestivalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    /// <summary>"All India" or a region (e.g. "South India") so a merchant can gauge relevance.</summary>
    public string Region { get; set; } = "All India";
    /// <summary>Campaign goal key this occasion maps to (usually "festival").</summary>
    public string SuggestedGoal { get; set; } = "festival";
    public string? Note { get; set; }
}
