namespace ecomm.api.Data.Entities;

/// <summary>A platform-wide broadcast shown to all merchants in their admin (platform-level; NOT tenant-scoped).</summary>
public class PlatformAnnouncement
{
    public long PlatformAnnouncementId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Level { get; set; } = "info";   // info | warning | critical
    public bool IsActive { get; set; } = true;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
