namespace ecomm.api.Data.Entities;

/// <summary>
/// A queued/finished reel render (Marketing Studio MS3·c). Enqueuing captures everything the render
/// needs (scenes as image URLs + durations + captions, narration to voice, aspect, music choice); a
/// background job then generates the voiceover, runs FFmpeg and stores the MP4. Marketing* cluster, no
/// FKs into core commerce tables.
/// </summary>
public class MarketingVideoRender : ITenantScoped
{
    public long MarketingVideoRenderId { get; set; }
    public long TenantId { get; set; }

    /// <summary>queued | rendering | done | failed.</summary>
    public string Status { get; set; } = "queued";

    public string Aspect { get; set; } = "9:16";
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>JSON array of scenes: [{imageUrl, duration, caption}].</summary>
    public string ScenesJson { get; set; } = "[]";
    public string? Narration { get; set; }
    public string LanguageCode { get; set; } = "en-IN";
    public string MusicMood { get; set; } = "upbeat";
    public bool IncludeMusic { get; set; }

    public string? OutputMediaUrl { get; set; }
    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
