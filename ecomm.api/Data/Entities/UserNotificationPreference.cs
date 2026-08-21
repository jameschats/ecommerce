namespace ecomm.api.Data.Entities;

/// <summary>A user's explicit consent state for one (Channel, Category) pair — e.g. "WhatsApp /
/// marketing = opted in on 2026-03-01". Absence of a row means no evidence of consent; the router
/// treats that as opted-out for marketing sends, never as opted-in.</summary>
public class UserNotificationPreference : ITenantScoped
{
    public long UserNotificationPreferenceId { get; set; }
    public long TenantId { get; set; } = 1;
    public long UserId { get; set; }
    public string Channel { get; set; } = string.Empty;
    /// <summary>"transactional" or "marketing" — transactional sends are never gated by this table.</summary>
    public string Category { get; set; } = "marketing";
    public bool IsOptedIn { get; set; }
    /// <summary>When consent was actually given — the compliance evidence, not just current state.</summary>
    public DateTime? OptedInAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}
