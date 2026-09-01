using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>
/// The weekly content plan (MS2 sub-step 2): propose a draft OUTLINE, review it (edit/add/remove
/// items), and confirm. Admin + feature-gated. Part of the Marketing Studio module (/api/marketing/*).
/// Generation of creatives from a confirmed plan arrives in sub-step 3.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[RequiresFeature("marketing_studio")]
[Route("api/marketing/plan")]
public sealed class MarketingPlanController(IMarketingPlanService plan) : ControllerBase
{
    [HttpPost("propose")]
    public async Task<IActionResult> Propose([FromBody] ProposePlanRequest? req, CancellationToken ct)
        => Ok(ApiResponse<PlanDto>.Ok(await plan.ProposeAsync(req ?? new ProposePlanRequest(null), ct), "Here's your proposed week — review and confirm."));

    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken ct)
        => Ok(ApiResponse<PlanDto?>.Ok(await plan.GetCurrentAsync(ct)));

    [HttpPut("items/{itemId:long}")]
    public async Task<IActionResult> UpdateItem(long itemId, UpdatePlanItemRequest req, CancellationToken ct)
        => Ok(ApiResponse<PlanItemDto>.Ok(await plan.UpdateItemAsync(itemId, req, ct), "Updated."));

    [HttpPost("items")]
    public async Task<IActionResult> AddItem(AddPlanItemRequest req, CancellationToken ct)
        => Ok(ApiResponse<PlanItemDto>.Ok(await plan.AddItemAsync(req, ct), "Added."));

    [HttpDelete("items/{itemId:long}")]
    public async Task<IActionResult> RemoveItem(long itemId, CancellationToken ct)
    {
        await plan.RemoveItemAsync(itemId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Removed."));
    }

    [HttpPost("{planId:long}/confirm")]
    public async Task<IActionResult> Confirm(long planId, CancellationToken ct)
        => Ok(ApiResponse<PlanDto>.Ok(await plan.ConfirmAsync(planId, ct), "Plan confirmed."));

    [HttpDelete("{planId:long}")]
    public async Task<IActionResult> Discard(long planId, CancellationToken ct)
    {
        await plan.DiscardAsync(planId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Discarded."));
    }
}
