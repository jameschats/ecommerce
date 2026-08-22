namespace ecomm.api.Data.Entities;

/// <summary>A merchant-configured event push target (v4 Phase 6 Track A) — e.g. "notify my app
/// whenever an order is created." <see cref="EncryptedSecret"/> is DataProtection-encrypted (needs
/// to be reversible to sign outgoing payloads, unlike an API key which only needs comparison).</summary>
public class WebhookSubscription : ITenantScoped
{
    public long WebhookSubscriptionId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Url { get; set; } = string.Empty;
    /// <summary>Comma-separated event types, e.g. "order.created,order.updated".</summary>
    public string Events { get; set; } = string.Empty;
    public string EncryptedSecret { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public long? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>One delivery attempt record — the full audit trail persists regardless of outcome,
/// same posture as every other audit log this v4 build shipped (notification history, pricing
/// suggestions).</summary>
public class WebhookDelivery : ITenantScoped
{
    public long WebhookDeliveryId { get; set; }
    public long TenantId { get; set; } = 1;
    public long WebhookSubscriptionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";   // Pending | Delivered | Failed | Exhausted
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public int? LastStatusCode { get; set; }
    public string? LastError { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public WebhookSubscription? Subscription { get; set; }
}
