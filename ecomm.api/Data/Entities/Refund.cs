namespace ecomm.api.Data.Entities;

public class Refund
{
    public long RefundId { get; set; }
    public long PaymentId { get; set; }
    public long OrderId { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";   // Pending | Processed | Failed
    public string? GatewayRefundId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
