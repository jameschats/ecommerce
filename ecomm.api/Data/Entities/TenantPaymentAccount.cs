namespace ecomm.api.Data.Entities;

/// <summary>A merchant's own payment account (Razorpay Route) — used in V2-5.</summary>
public class TenantPaymentAccount : ITenantScoped
{
    public long TenantPaymentAccountId { get; set; }
    public long TenantId { get; set; }
    public string Provider { get; set; } = "Razorpay";
    public string? AccountId { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
