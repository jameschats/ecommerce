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

    [HttpPost("select-plan")]
    public async Task<IActionResult> SelectPlan([FromBody] SelectPlanRequest request, CancellationToken ct)
    {
        var sub = await subscriptions.SelectPlanAsync(request.PlanId, ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(sub, "Plan selected."));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        await subscriptions.CancelAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Subscription cancelled."));
    }
}

public sealed record SelectPlanRequest(int PlanId);
