namespace ecomm.api.Data.Entities;

/// <summary>Append-only record of a charge to a merchant (written by the billing webhook).</summary>
public class TenantBillingHistory : ITenantScoped
{
    public long TenantBillingHistoryId { get; set; }
    public long TenantId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RazorpayPaymentId { get; set; }
    public DateTime BilledAt { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; }
}
