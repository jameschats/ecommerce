using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Subscriptions;

/// <summary>Merchant: view + manage the store's own subscription (tenant-scoped).</summary>
[ApiController]
[Route("api/subscription")]
[Authorize(Roles = "Admin")]
public sealed class SubscriptionController(ISubscriptionService subscriptions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        var sub = await subscriptions.GetCurrentAsync(ct);
        return Ok(ApiResponse<SubscriptionDto?>.Ok(sub));
    }

    [HttpGet("billing-history")]
    public async Task<IActionResult> BillingHistory(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<BillingHistoryDto>>.Ok(await subscriptions.GetBillingHistoryAsync(ct)));

    [HttpPost("select-plan")]
    public async Task<IActionResult> SelectPlan([FromBody] SelectPlanRequest request, CancellationToken ct)
    {
        var sub = await subscriptions.SelectPlanAsync(request.PlanId, ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(sub, "Plan selected."));
    }

    /// <summary>Create a Razorpay order for one billing cycle; the browser then opens checkout.</summary>
    [HttpPost("checkout/start")]
    public async Task<IActionResult> StartCheckout([FromBody] SelectPlanRequest request, CancellationToken ct)
        => Ok(ApiResponse<CheckoutSessionDto>.Ok(await subscriptions.StartCheckoutAsync(request.PlanId, ct)));

    /// <summary>Verify the payment signature, record the charge and activate the plan.</summary>
    [HttpPost("checkout/confirm")]
    public async Task<IActionResult> ConfirmCheckout([FromBody] ConfirmCheckoutCommand cmd, CancellationToken ct)
        => Ok(ApiResponse<SubscriptionDto>.Ok(await subscriptions.ConfirmCheckoutAsync(cmd, ct), "Payment received — plan active."));

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        await subscriptions.CancelAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Subscription cancelled."));
    }

    /// <summary>Set up recurring auto-pay for a plan. Returns an auth URL (redirect to authorize the mandate)
    /// or an already-active mandate (dev/Mock).</summary>
    [HttpPost("autopay/setup")]
    public async Task<IActionResult> SetupAutoPay([FromBody] SelectPlanRequest request, CancellationToken ct)
    {
        var result = await subscriptions.SetupAutoPayAsync(request.PlanId, ct);
        return Ok(ApiResponse<AutoPaySetupDto>.Ok(result, result.Active ? "Auto-pay is on." : "Authorize the mandate to finish."));
    }

    [HttpPost("autopay/cancel")]
    public async Task<IActionResult> CancelAutoPay(CancellationToken ct)
    {
        await subscriptions.CancelAutoPayAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Auto-pay will stop at the end of the current cycle."));
    }
}

public sealed record SelectPlanRequest(int PlanId);
