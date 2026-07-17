namespace ecomm.api.Data.Entities;

/// <summary>
/// A tenant's AI credit balance. Credits are abstract, per-action units (see AiCreditPricing) that the
/// platform grants each billing cycle (Plan.AiCredits) and the merchant can top up (AiCreditPacks).
/// Every debit/grant is also written to <see cref="AiUsageLog"/> as an audit trail; <c>Balance</c> is the
/// authoritative running total.
/// </summary>
public class TenantAiCredit : ITenantScoped
{
    public long TenantAiCreditId { get; set; }
    public long TenantId { get; set; }
    public int Balance { get; set; }
    public int CycleGrant { get; set; }         // credits granted for the current cycle (from the plan)
    public DateTime? CycleResetAt { get; set; } // when the cycle grant refreshes (= subscription period end)
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
