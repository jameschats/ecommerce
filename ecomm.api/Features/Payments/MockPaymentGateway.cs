namespace ecomm.api.Features.Payments;

/// <summary>
/// Deterministic in-process gateway for development: no external calls, no keys.
/// Mirrors Razorpay's shape so the full money path is testable, and swapping in
/// <see cref="RazorpayPaymentGateway"/> later needs no changes to OrderService.
/// </summary>
public sealed class MockPaymentGateway : IPaymentGateway
{
    public string Name => "Mock";
    public string? PublicKey => null;

    public Task<GatewayOrder> CreateOrderAsync(long orderId, decimal amount, string currency, string receipt, CancellationToken ct = default)
        => Task.FromResult(new GatewayOrder($"mock_order_{orderId:D8}_{Guid.NewGuid():N}".Substring(0, 32), amount, currency));

    // The mock "client" doesn't produce a real signature; any confirmation succeeds.
    public bool VerifySignature(string gatewayOrderId, string gatewayPaymentId, string signature) => true;

    public Task<GatewayRefund> RefundAsync(string gatewayPaymentId, decimal amount, CancellationToken ct = default)
        => Task.FromResult(new GatewayRefund($"mock_rfnd_{Guid.NewGuid():N}".Substring(0, 24), "processed"));
}
