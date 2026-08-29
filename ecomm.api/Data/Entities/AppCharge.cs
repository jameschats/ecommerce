namespace ecomm.api.Data.Entities;

/// <summary>
/// A charge for a paid app (App Store S4). Tenant-scoped. For a one-time app the merchant is charged once
/// at install; for a recurring app the charge is created per cycle (recurring collection rides the same
/// Razorpay-Subscriptions rail as platform billing — enabled once that is live). Revenue-share splits the
/// fee between the platform and the developer (paid out via Razorpay Route).
/// </summary>
public class AppCharge : ITenantScoped
{
    public long AppChargeId { get; set; }
    public long TenantId { get; set; }
    public long AppId { get; set; }
    public long AppInstallationId { get; set; }
    public string Type { get; set; } = "onetime";       // onetime | recurring | usage
    public decimal Amount { get; set; }                  // total charged to the merchant (GST-inclusive)
    public decimal PlatformFee { get; set; }             // platform's revenue share
    public decimal DeveloperShare { get; set; }          // paid to the developer (via Route)
    public string Status { get; set; } = "pending";      // pending | paid | failed | refunded
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
