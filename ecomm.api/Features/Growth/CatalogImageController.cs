using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Growth;

/// <summary>
/// Admin: one-time backfill of real, per-item catalog images (studio-shot style) for categories/products
/// that currently have no image or only a generic shared sample-catalog placeholder. Not gated behind
/// the "growth" plan feature like <see cref="GrowthController"/>'s marketing tools — this is catalog
/// upkeep, not marketing content, and AI credits are already the natural metering point.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/catalog-images")]
public sealed class CatalogImageController(ICatalogImageService images) : ControllerBase
{
    /// <summary>Runs the backfill for up to <paramref name="maxItems"/> eligible categories/products
    /// (categories first), stopping early if AI credits run out. Safe to call again — anything already
    /// given a real generated image no longer matches the eligibility check.</summary>
    [HttpPost("backfill")]
    public async Task<IActionResult> Backfill([FromQuery] int maxItems, CancellationToken ct)
        => Ok(ApiResponse<CatalogImageBackfillResult>.Ok(await images.BackfillAsync(maxItems <= 0 ? 15 : maxItems, ct), "Backfill complete."));
}
