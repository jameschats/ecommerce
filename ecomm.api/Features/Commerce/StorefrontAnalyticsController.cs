using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Commerce;

/// <summary>Merchant storefront analytics from behavioural events (Track E): funnel, top viewed, searches.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/analytics")]
public sealed class StorefrontAnalyticsController(IStorefrontAnalyticsService svc) : ControllerBase
{
    [HttpGet("storefront")]
    public async Task<IActionResult> Storefront([FromQuery] int days = 30, CancellationToken ct = default)
        => Ok(ApiResponse<StorefrontAnalyticsDto>.Ok(await svc.GetAsync(days, ct)));
}
