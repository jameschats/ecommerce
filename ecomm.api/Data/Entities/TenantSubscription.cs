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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Plan? Plan { get; set; }
}
