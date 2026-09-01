namespace ecomm.api.Data.Entities;

/// <summary>
/// A tenant's weekly-plan cadence preferences (Marketing Studio MS2). Drives how many of each creative
/// type the AI proposes per week, when they post, and whether next week is auto-drafted. One row per
/// tenant, created lazily. Marketing* cluster, no FKs into core commerce tables.
/// </summary>
public class MarketingPlanSettings : ITenantScoped
{
    public long MarketingPlanSettingsId { get; set; }
    public long TenantId { get; set; }

    public int TextPerWeek { get; set; } = 3;
    public int PostersPerWeek { get; set; } = 2;
    public int VideosPerWeek { get; set; } = 0;      // videos land in MS3; inert until then

    /// <summary>0 = Sunday … 6 = Saturday. Default Monday.</summary>
    public int WeekStartDay { get; set; } = 1;

    /// <summary>Default hour of day (0–23, tenant-local intent) the AI slots posts at.</summary>
    public int DefaultPostHour { get; set; } = 10;

    /// <summary>When on, a weekly job drafts next week's plan from these prefs (left at Draft for confirm).</summary>
    public bool AutoRecur { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Per-tenant, per-platform channel preference for the weekly plan (MS2). Lets a merchant enable a
/// connected channel and choose which creative types go there — the "uncheck poster for Instagram"
/// control. One row per (tenant, platform); only connected channels are surfaced in the UI.
/// </summary>
public class MarketingChannelPref : ITenantScoped
{
    public long MarketingChannelPrefId { get; set; }
    public long TenantId { get; set; }

    public string Platform { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool AllowText { get; set; } = true;
    public bool AllowPoster { get; set; } = true;
    public bool AllowVideo { get; set; } = true;

    public DateTime? UpdatedAt { get; set; }
}
