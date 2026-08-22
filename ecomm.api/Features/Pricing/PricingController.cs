using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Pricing;

/// <summary>
/// Dynamic Pricing (v4 Phase 5) — approval-mode only, no exceptions. Gated behind the
/// "dynamic-pricing" plan feature via the same <see cref="RequiresFeatureAttribute"/>/
/// <c>IEntitlementService</c> mechanism Growth already uses (this was already real, generic,
/// server-side-enforced infrastructure — not something this phase needed to build from scratch).
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[RequiresFeature("dynamic-pricing")]
[Route("api/admin/pricing")]
public sealed class PricingController(
    IPricingControlsService controls, IPricingEngineService engine, IPricingSuggestionService suggestions) : ControllerBase
{
    private long UserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    // ----- Per-product controls -----
    [HttpGet("controls")]
    public async Task<IActionResult> ListControls([FromQuery] long? categoryId, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ProductPricingControlsDto>>.Ok(await controls.ListControlsAsync(categoryId, ct)));

    [HttpPut("controls/{productId:long}")]
    public async Task<IActionResult> SetControls(long productId, SetPricingControlsRequest request, CancellationToken ct)
        => Ok(ApiResponse<ProductPricingControlsDto>.Ok(await controls.SetAsync(productId, request, ct), "Pricing bounds saved."));

    [HttpPost("controls/bulk-bounds")]
    public async Task<IActionResult> BulkBounds(BulkBoundsRequest request, CancellationToken ct)
        => Ok(ApiResponse<object>.Ok(new { updated = await controls.BulkSetBoundsAsync(request, ct) }, "Bounds applied."));

    // ----- Season rules -----
    [HttpGet("season-rules")]
    public async Task<IActionResult> ListSeasonRules(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<PricingSeasonRuleDto>>.Ok(await controls.ListSeasonRulesAsync(ct)));

    [HttpPost("season-rules")]
    public async Task<IActionResult> SaveSeasonRule(SavePricingSeasonRuleRequest request, CancellationToken ct)
        => Ok(ApiResponse<PricingSeasonRuleDto>.Ok(await controls.SaveSeasonRuleAsync(request, ct), "Season rule saved."));

    [HttpDelete("season-rules/{id:long}")]
    public async Task<IActionResult> DeleteSeasonRule(long id, CancellationToken ct)
    {
        await controls.DeleteSeasonRuleAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Season rule removed."));
    }

    // ----- Suggestions -----
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateNow(CancellationToken ct)
        => Ok(ApiResponse<object>.Ok(new { generated = await engine.GenerateSuggestionsForCurrentTenantAsync(ct) }, "Checked for new suggestions."));

    [HttpGet("suggestions")]
    public async Task<IActionResult> ListSuggestions(
        [FromQuery] string? status, [FromQuery] long? productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<PriceSuggestionDto>>.Ok(await suggestions.ListAsync(status, productId, page, pageSize, ct)));

    [HttpPost("suggestions/{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken ct)
        => Ok(ApiResponse<PriceSuggestionDto>.Ok(await suggestions.ApproveAsync(id, UserId, ct), "Price updated."));

    [HttpPost("suggestions/{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken ct)
        => Ok(ApiResponse<PriceSuggestionDto>.Ok(await suggestions.RejectAsync(id, ct), "Suggestion dismissed."));
}
