using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>
/// The Creative Library (/admin/marketing/library) — every generated poster/text post in one browsable
/// place, whichever flow made it. Admin + feature-gated; part of the Marketing Studio module. Assigning
/// a channel to a library item reuses POST /api/marketing/plan/items/{id}/schedule.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[RequiresFeature("marketing_studio")]
[Route("api/marketing/library")]
public sealed class MarketingLibraryController(IMarketingLibraryService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? type, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<LibraryItemDto>>.Ok(await svc.ListAsync(type, ct)));
}
