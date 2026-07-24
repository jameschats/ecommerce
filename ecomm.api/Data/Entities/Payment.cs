namespace ecomm.api.Data.Entities;

public class Payment
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

    // --- Manual UPI / bank transfer (design.md §8) ---
    /// <summary>UPI or bank reference the buyer supplies. Their claim, not our confirmation.</summary>
    public string? ReferenceNumber { get; set; }
    public string? ProofImageUrl { get; set; }
    /// <summary>When the buyer said they paid.</summary>
    public DateTime? ReportedAt { get; set; }
    /// <summary>When the shop verified the money arrived. Kept distinct from ReportedAt.</summary>
    public DateTime? ConfirmedAt { get; set; }
    public long? ConfirmedBy { get; set; }
}
