namespace ecomm.api.Data.Entities;

/// <summary>Which pair of parties a conversation is between.</summary>
public static class ConversationAxis
{
    /// <summary>A shopper talking to the store that sells to them.</summary>
    public const string ShopperMerchant = "ShopperMerchant";

    /// <summary>A merchant talking to the platform operator (the original support ticket).</summary>
    public const string MerchantPlatform = "MerchantPlatform";
}

/// <summary>Who wrote a message. Replaces the old two-party <c>FromPlatform</c> boolean.</summary>
public static class MessageAuthorType
{
    public const string Shopper = "Shopper";
    public const string Merchant = "Merchant";
    public const string Platform = "Platform";
}

/// <summary>
/// A support conversation. Tenant-scoped; the platform works the merchant↔platform axis
/// cross-tenant via IgnoreQueryFilters.
/// </summary>
public class SupportTicket : ITenantScoped
{
    public long SupportTicketId { get; set; }
    public long TenantId { get; set; } = 1;

    /// <summary>See <see cref="ConversationAxis"/>. Existing rows are all merchant↔platform.</summary>
    public string Axis { get; set; } = ConversationAxis.MerchantPlatform;

    /// <summary>Human-quotable id, e.g. <c>TKT-2026-00042</c>.</summary>
    public string? Reference { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";       // Open | Pending | Closed
    public string Priority { get; set; } = "Normal";   // Low | Normal | High | Urgent
    public string? Category { get; set; }

    public long? CreatedByUserId { get; set; }
    public long? AssignedToUserId { get; set; }

    // --- Shopper↔merchant only ---
    /// <summary>Set when the shopper is signed in; null for an anonymous enquiry.</summary>
    public long? ShopperUserId { get; set; }
    /// <summary>Always set on a shopper thread — the reply address, and the anonymous identity.</summary>
    public string? ShopperEmail { get; set; }
    public long? OrderId { get; set; }
    public long? ProductId { get; set; }

    public DateTime? LastMessageAt { get; set; }
    /// <summary>When the other side first replied — the number merchants are actually judged on.</summary>
    public DateTime? FirstResponseAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Superseded by <see cref="Axis"/> + the first message's author. Retained only so the
    /// pre-251 binary keeps working during a deploy; drop in a later migration.
    /// </summary>
    public bool OpenedByPlatform { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>A message on a conversation. <see cref="IsInternalNote"/> messages are platform-only.</summary>
public class SupportMessage : ITenantScoped
{
    public long SupportMessageId { get; set; }
    public long TenantId { get; set; } = 1;
    public long SupportTicketId { get; set; }
    public long? AuthorUserId { get; set; }

    /// <summary>See <see cref="MessageAuthorType"/>.</summary>
    public string AuthorType { get; set; } = MessageAuthorType.Merchant;

    public bool IsInternalNote { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Superseded by <see cref="AuthorType"/>. Still written so a rollback to the pre-251
    /// binary reads correct data; drop in a later migration.
    /// </summary>
    public bool FromPlatform { get; set; }
}
