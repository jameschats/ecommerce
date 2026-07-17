using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Features.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Ai;

/// <summary>Merchant: AI credit balance, usage history, and buy-credits top-ups (paid to the platform).</summary>
[ApiController]
[Route("api/admin/ai")]
[Authorize(Roles = "Admin")]
public sealed class AiCreditController(IAiCreditService credits, PlatformPaymentGatewayFactory gateways) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Balance(CancellationToken ct)
        => Ok(ApiResponse<AiBalanceDto>.Ok(await credits.GetBalanceAsync(ct)));

    [HttpGet("usage")]
    public async Task<IActionResult> Usage(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<AiUsageDto>>.Ok(await credits.GetUsageAsync(50, ct)));

    /// <summary>
    /// Start a credit top-up. The Mock gateway (dev / no platform gateway configured) grants instantly;
    /// Razorpay returns an order for the browser checkout widget, confirmed via <see cref="Verify"/>.
    /// </summary>
    [HttpPost("topup")]
    public async Task<IActionResult> TopUp([FromBody] TopUpRequest req, CancellationToken ct)
    {
        var pack = await credits.GetPackAsync(req.PackId, ct) ?? throw new AppException("Credit pack not found.", 404);
        var gateway = gateways.Create();
        var order = await gateway.CreateOrderAsync(0, pack.PriceInr, "INR", $"ai-credits-{pack.AiCreditPackId}", ct);

        if (gateway.Name == "Mock")
        {
            var balance = await credits.TopUpAsync(pack.AiCreditPackId, order.GatewayOrderId, ct);
            return Ok(ApiResponse<TopUpResultDto>.Ok(new TopUpResultDto(true, balance, null, null, null, null), "Credits added."));
        }

        var amountPaise = (long)Math.Round(pack.PriceInr * 100m, MidpointRounding.AwayFromZero);
        return Ok(ApiResponse<TopUpResultDto>.Ok(
            new TopUpResultDto(false, 0, order.GatewayOrderId, gateway.PublicKey, amountPaise, "INR")));
    }

    /// <summary>Confirm a Razorpay top-up after the browser widget succeeds: verify the signature, grant credits.</summary>
    [HttpPost("topup/verify")]
    public async Task<IActionResult> Verify([FromBody] TopUpVerifyRequest req, CancellationToken ct)
    {
        var gateway = gateways.Create();
        if (!gateway.VerifySignature(req.GatewayOrderId, req.GatewayPaymentId, req.Signature))
            throw new AppException("Payment verification failed.", 400);
        var balance = await credits.TopUpAsync(req.PackId, req.GatewayPaymentId, ct);
        return Ok(ApiResponse<TopUpResultDto>.Ok(new TopUpResultDto(true, balance, null, null, null, null), "Credits added."));
    }
}

public sealed record TopUpRequest(int PackId);
public sealed record TopUpVerifyRequest(int PackId, string GatewayOrderId, string GatewayPaymentId, string Signature);
public sealed record TopUpResultDto(bool Granted, int Balance, string? GatewayOrderId, string? KeyId, long? AmountPaise, string? Currency);
