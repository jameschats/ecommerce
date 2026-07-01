namespace ecomm.api.Data.Entities;

/// <summary>An admin-editable message template (Email/SMS/WhatsApp) with {{token}} placeholders.</summary>
public class NotificationTemplate
{
    public long NotificationTemplateId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Code { get; set; } = string.Empty;
    public string Channel { get; set; } = "Email";
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
