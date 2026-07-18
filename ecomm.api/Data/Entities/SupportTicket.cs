namespace ecomm.api.Data.Entities;

/// <summary>A merchant support ticket. Tenant-scoped; the platform works these cross-tenant via IgnoreQueryFilters.</summary>
public class SupportTicket : ITenantScoped
{
    public long SupportTicketId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";   // Open | Pending | Closed
    public long? CreatedByUserId { get; set; }
    public bool OpenedByPlatform { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>A message on a support ticket. <see cref="IsInternalNote"/> messages are platform-only.</summary>
public class SupportMessage : ITenantScoped
{
    public long SupportMessageId { get; set; }
    public long TenantId { get; set; } = 1;
    public long SupportTicketId { get; set; }
    public long? AuthorUserId { get; set; }
    public bool FromPlatform { get; set; }
    public bool IsInternalNote { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
