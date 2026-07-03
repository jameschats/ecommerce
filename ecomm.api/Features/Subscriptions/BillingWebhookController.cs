using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Subscriptions;

/// <summary>
/// Receives billing charge events (the platform collecting from merchants).
/// Idempotent on the payment id. Guarded by a shared secret: if
/// <c>Billing:WebhookSecret</c> is configured, the <c>X-Webhook-Secret</c> header
/// must match; in dev (no secret set) it's open for testing.
///
/// PROD: replace the shared-secret guard with Razorpay HMAC signature verification
/// (X-Razorpay-Signature) and map the subscription id → tenant.
/// </summary>
[ApiController]
[Route("api/webhooks/billing")]
[AllowAnonymous]
public sealed class BillingWebhookController(ISubscriptionService subscriptions, IConfiguration config, ILogger<BillingWebhookController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Charge([FromBody] BillingWebhookPayload payload, CancellationToken ct)
    {
        var secret = config["Billing:WebhookSecret"];
        if (!string.IsNullOrEmpty(secret))
        {
            var provided = Request.Headers["X-Webhook-Secret"].ToString();
            if (provided != secret) return Unauthorized(ApiResponse<object>.Fail("Invalid webhook signature."));
        }

        if (payload.TenantId <= 0 || string.IsNullOrWhiteSpace(payload.RazorpayPaymentId))
            return BadRequest(ApiResponse<object>.Fail("TenantId and RazorpayPaymentId are required."));

        var start = payload.PeriodStart ?? DateTime.UtcNow;
        var end = payload.PeriodEnd ?? start.AddMonths(1);
        var recorded = await subscriptions.RecordChargeAsync(
            new RecordChargeCommand(payload.TenantId, payload.Amount, payload.RazorpayPaymentId,
                payload.RazorpaySubscriptionId, start, end), ct);

        logger.LogInformation("Billing webhook for tenant {TenantId} payment {PaymentId}: {Outcome}",
            payload.TenantId, payload.RazorpayPaymentId, recorded ? "recorded" : "duplicate-ignored");

        return Ok(ApiResponse<object>.Ok(new { recorded }, recorded ? "Charge recorded." : "Already processed."));
    }
}

public sealed record BillingWebhookPayload(
    long TenantId, decimal Amount, string RazorpayPaymentId,
    string? RazorpaySubscriptionId, DateTime? PeriodStart, DateTime? PeriodEnd);
