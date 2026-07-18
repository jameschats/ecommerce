using System.Text;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Subscriptions;

/// <summary>
/// Razorpay-native billing webhook. Point the Razorpay dashboard webhook at this URL and subscribe
/// to <c>order.paid</c>, using <c>Billing:WebhookSecret</c> as the webhook secret.
///
/// The body is read RAW (no model binding) because the signature is an HMAC over the exact bytes.
/// Anything other than a signature failure returns 200 so Razorpay doesn't retry events we ignore.
/// </summary>
[ApiController]
[Route("api/webhooks/razorpay")]
[AllowAnonymous]
public sealed class RazorpayWebhookController(IRazorpayWebhookService webhook) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var raw = await reader.ReadToEndAsync(ct);

        if (!webhook.VerifySignature(raw, Request.Headers["X-Razorpay-Signature"].ToString()))
            return Unauthorized(ApiResponse<object>.Fail("Invalid webhook signature."));

        var outcome = await webhook.HandleAsync(raw, ct);
        return Ok(ApiResponse<object>.Ok(new { handled = outcome.Handled }, outcome.Message));
    }
}
