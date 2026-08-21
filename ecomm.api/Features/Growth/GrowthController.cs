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
public sealed class GrowthController(
    IGrowthGenerationService gen, IBrandKitService brandKit, IGrowthCampaignService campaigns,
    IGrowthImageService images) : ControllerBase
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
        => Ok(ApiResponse<GrowthContentDto>.Ok(await gen.GenerateAsync(request, UserId, ct: ct), "Generated."));

    [HttpGet("content")]
    public async Task<IActionResult> Library(
        [FromQuery] string? contentType, [FromQuery] long? productId, [FromQuery] long? campaignId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<GrowthContentDto>>.Ok(await gen.LibraryAsync(contentType, productId, campaignId, from, to, page, pageSize, ct)));

    [HttpPut("content/{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateContentRequest request, CancellationToken ct)
        => Ok(ApiResponse<GrowthContentDto>.Ok(await gen.UpdateAsync(id, request.Body, request.Title, request.Status, ct), "Saved."));

    [HttpDelete("content/{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await gen.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Removed."));
    }

    // ---- Campaigns (G2) ----

    [HttpGet("goals")]
    public IActionResult Goals() => Ok(ApiResponse<IReadOnlyList<GoalDto>>.Ok(campaigns.Goals()));

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign(CreateCampaignRequest request, CancellationToken ct)
        => Ok(ApiResponse<CampaignDto>.Ok(await campaigns.CreateAsync(request, UserId, ct), "Campaign generated."));

    [HttpGet("campaigns")]
    public async Task<IActionResult> Campaigns([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<CampaignSummaryDto>>.Ok(await campaigns.ListAsync(page, pageSize, ct)));

    [HttpGet("campaigns/{id:long}")]
    public async Task<IActionResult> Campaign(long id, CancellationToken ct)
        => Ok(ApiResponse<CampaignDto>.Ok(await campaigns.GetAsync(id, ct)));

    [HttpDelete("campaigns/{id:long}")]
    public async Task<IActionResult> DeleteCampaign(long id, CancellationToken ct)
    {
        await campaigns.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Campaign removed."));
    }

    // ---- Image generation (POC) ----

    [HttpGet("image/styles")]
    public IActionResult ImageStyles() => Ok(ApiResponse<IReadOnlyList<ImageStyleDto>>.Ok(images.Styles()));

    [HttpGet("image/formats")]
    public IActionResult ImageFormats() => Ok(ApiResponse<IReadOnlyList<ImageFormatDto>>.Ok(images.Formats()));

    [HttpPost("image")]
    public async Task<IActionResult> GenerateImage(GenerateImageRequest request, CancellationToken ct)
        => Ok(ApiResponse<GeneratedImageDto>.Ok(await images.GenerateAsync(request, UserId, ct), "Image generated."));

    [HttpGet("image/recent")]
    public async Task<IActionResult> RecentImages(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<GeneratedImageDto>>.Ok(await images.RecentAsync(ct)));
}

public sealed record UpdateContentRequest(string Body, string? Title, string Status);
