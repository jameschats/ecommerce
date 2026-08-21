namespace ecomm.api.Data.Entities;

/// <summary>An admin-editable message template (Email/SMS/WhatsApp) with {{token}} placeholders.</summary>
public class NotificationTemplate : ITenantScoped
{
    public long NotificationTemplateId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Code { get; set; } = string.Empty;
    public string Channel { get; set; } = "Email";
    public string? Subject { get; set; }
    public string? Body { get; set; }
    /// <summary>BSP-assigned template id (e.g. Gupshup's template GUID) — only meaningful when
    /// Channel="WhatsApp", since Meta requires referencing a pre-approved template by id rather
    /// than sending the rendered text directly like Email/SMS do.</summary>
    public string? ExternalTemplateId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
