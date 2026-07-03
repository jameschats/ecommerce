namespace ecomm.api.Data.Entities;

public class Payment : ITenantScoped
{
    public long PaymentId { get; set; }
    public long TenantId { get; set; } = 1;
    public long OrderId { get; set; }
    public string Method { get; set; } = "Razorpay";   // Razorpay | COD
    public string Status { get; set; } = "Pending";     // Pending|Success|Failed|Refunded|PartiallyRefunded
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
