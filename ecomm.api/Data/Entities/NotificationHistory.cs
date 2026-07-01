namespace ecomm.api.Data.Entities;

/// <summary>An audit row for every notification we attempted to send (Email/SMS).</summary>
public class NotificationHistory
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
}
