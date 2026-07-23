using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Growth;

/// <summary>
/// AI Growth — marketing content generation (G1). Gated behind the <c>growth</c> plan feature;
/// a merchant without it gets 402 and the UI shows an upgrade card instead of the tools.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[RequiresFeature("growth")]
[Route("api/admin/growth")]
public sealed class GrowthController(IGrowthGenerationService gen, IBrandKitService brandKit) : ControllerBase
{
    private long? UserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet("types")]
    public IActionResult Types() => Ok(ApiResponse<IReadOnlyList<GrowthTypeDto>>.Ok(gen.Types()));

    [HttpGet("brand-kit")]
    public async Task<IActionResult> GetBrandKit(CancellationToken ct)
        => Ok(ApiResponse<BrandKitDto>.Ok(await brandKit.GetAsync(ct)));

    [HttpPut("brand-kit")]
    public async Task<IActionResult> SaveBrandKit(BrandKitDto request, CancellationToken ct)
        => Ok(ApiResponse<BrandKitDto>.Ok(await brandKit.SaveAsync(request, ct), "Brand kit saved."));

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(GenerateRequest request, CancellationToken ct)
        => Ok(ApiResponse<GrowthContentDto>.Ok(await gen.GenerateAsync(request, UserId, ct), "Generated."));

    [HttpGet("content")]
    public async Task<IActionResult> Library(
        [FromQuery] string? contentType, [FromQuery] long? productId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<GrowthContentDto>>.Ok(await gen.LibraryAsync(contentType, productId, page, pageSize, ct)));

    [HttpPut("content/{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateContentRequest request, CancellationToken ct)
        => Ok(ApiResponse<GrowthContentDto>.Ok(await gen.UpdateAsync(id, request.Body, request.Title, request.Status, ct), "Saved."));

    [HttpDelete("content/{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await gen.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Removed."));
    }
}

public sealed record UpdateContentRequest(string Body, string? Title, string Status);
