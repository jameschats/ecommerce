namespace ecomm.api.Features.Payments;

public sealed record GatewayOrder(string GatewayOrderId, decimal Amount, string Currency);
public sealed record GatewayRefund(string GatewayRefundId, string Status);

/// <summary>
/// Payment provider abstraction. Two implementations:
///   * <c>MockPaymentGateway</c> — dev default; simulates the Razorpay flow deterministically.
///   * <c>RazorpayPaymentGateway</c> — used when Payments:Provider=Razorpay and keys are set.
/// Flow: CreateOrder → (client pays) → VerifySignature → (Capture) → optionally Refund.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Gateway name persisted on PaymentTransactions (e.g. "Mock", "Razorpay").</summary>
    string Name { get; }

    /// <summary>Public key handed to the browser widget (Razorpay key_id); null for mock.</summary>
    string? PublicKey { get; }

    Task<GatewayOrder> CreateOrderAsync(long orderId, decimal amount, string currency, string receipt, CancellationToken ct = default);

    bool VerifySignature(string gatewayOrderId, string gatewayPaymentId, string signature);

    Task<GatewayRefund> RefundAsync(string gatewayPaymentId, decimal amount, CancellationToken ct = default);
}

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";
    public string Provider { get; set; } = "Mock";   // Mock | Razorpay
    public string? RazorpayKeyId { get; set; }
    public string? RazorpayKeySecret { get; set; }
}
