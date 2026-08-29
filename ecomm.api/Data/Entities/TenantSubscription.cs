namespace ecomm.api.Data.Entities;

/// <summary>A tenant's current subscription. State: Trial | Active | Suspended | Cancelled.</summary>
public class TenantSubscription : ITenantScoped
{
    public long TenantSubscriptionId { get; set; }
    public long TenantId { get; set; }
    public int PlanId { get; set; }
    public string Status { get; set; } = "Trial";
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? GraceEndsAt { get; set; }
    /// <summary>Which "trial ends in N days" reminder was last sent (7 | 3 | 1), so each fires once. Null = none yet.</summary>
    public int? TrialReminderStage { get; set; }
    public string? RazorpaySubscriptionId { get; set; }

    // --- P2 recurring auto-debit (Razorpay Subscriptions) ---
    public string? RazorpayCustomerId { get; set; }
    /// <summary>none | pending | active | halted | cancelled — the auto-pay mandate state.</summary>
    public string MandateStatus { get; set; } = "none";
    /// <summary>Display-only method summary, e.g. "Visa •••• 4242" / "UPI autopay". Never a PAN.</summary>
    public string? PaymentMethodSummary { get; set; }
    /// <summary>Next scheduled auto-debit (from Razorpay).</summary>
    public DateTime? NextChargeAt { get; set; }
    /// <summary>Merchant asked to cancel — stop after the current cycle.</summary>
    public bool CancelAtPeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Plan? Plan { get; set; }
}
