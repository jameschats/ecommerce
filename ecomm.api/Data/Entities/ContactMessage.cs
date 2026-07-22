namespace ecomm.api.Data.Entities;

/// <summary>
/// A message sent from the storefront contact form — the first shopper→merchant channel.
/// Written by anonymous visitors, so the endpoint that creates these is rate-limited and
/// honeypot-guarded; nothing here is trusted, and everything is HTML-escaped on display.
/// </summary>
public class ContactMessage : ITenantScoped
{
    public long ContactMessageId { get; set; }
    public long TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;

    /// <summary>Storefront page the message was sent from — useful context when replying.</summary>
    public string? SourceUrl { get; set; }

    public string Status { get; set; } = "New";   // New | Handled
    public long? HandledByUserId { get; set; }
    public DateTime? HandledAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
