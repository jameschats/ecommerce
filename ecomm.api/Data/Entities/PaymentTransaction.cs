namespace ecomm.api.Data.Entities;

public class PaymentTransaction
{
    public long PaymentTransactionId { get; set; }
    public long PaymentId { get; set; }
    public string Gateway { get; set; } = "Razorpay";
    public string? GatewayOrderId { get; set; }
    public string? GatewayPaymentId { get; set; }
    public string? GatewaySignature { get; set; }
    public string TransactionType { get; set; } = string.Empty;   // Authorize | Capture | Refund
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RawResponse { get; set; }
    public DateTime CreatedAt { get; set; }
}
