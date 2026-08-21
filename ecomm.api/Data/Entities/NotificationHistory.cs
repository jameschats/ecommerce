namespace ecomm.api.Data.Entities;

/// <summary>An audit row for every notification we attempted to send (Email/SMS).</summary>
public class NotificationHistory : ITenantScoped
{
    public long NotificationHistoryId { get; set; }
    public long TenantId { get; set; } = 1;
    public long? TemplateId { get; set; }
    public string Channel { get; set; } = "Email";
    public string Recipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Error { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Groups every attempt (primary + fallback channels) belonging to one logical
    /// notification send — null for rows written before the router shipped.</summary>
    public string? AttemptGroupId { get; set; }
    /// <summary>1 = primary channel, 2 = first fallback, etc. Always 1 for pre-router rows.</summary>
    public int AttemptNumber { get; set; } = 1;
}
