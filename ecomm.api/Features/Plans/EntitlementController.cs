using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Plans;

/// <summary>What the merchant's plan allows and how much they've used.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/plan-usage")]
public sealed class EntitlementController(IEntitlementService entitlements) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Usage(CancellationToken ct)
        => Ok(ApiResponse<PlanUsageDto>.Ok(await entitlements.GetUsageAsync(ct)));
}
