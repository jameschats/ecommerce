using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.MarketingStudio;

public sealed record SuggestHeadlineRequest(long? ProductId, string? Topic);
public sealed record GenerateBackgroundRequest(PosterStudioRequest Poster, string Style);

/// <summary>
/// The standalone Poster Studio — an editor for crafting one poster deliberately (org or product-led,
/// full control over headline/price/CTA/logo/name), separate from the weekly-plan batch flow. Preview
/// is free/instant; Create is the one metered step. Admin + feature-gated; part of the Marketing
/// Studio module (/api/marketing/*).
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[RequiresFeature("marketing_studio")]
[Route("api/marketing/poster")]
public sealed class PosterStudioController(IPosterStudioService svc) : ControllerBase
{
    private long? UserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(PosterStudioRequest req, CancellationToken ct)
        => Ok(ApiResponse<PosterPreviewResult>.Ok(await svc.PreviewAsync(req, ct)));

    [HttpPost("suggest-headline")]
    public async Task<IActionResult> SuggestHeadline(SuggestHeadlineRequest req, CancellationToken ct)
        => Ok(ApiResponse<SuggestHeadlineResult>.Ok(await svc.SuggestHeadlineAsync(req.ProductId, req.Topic, ct)));

    [HttpGet("background-styles")]
    public IActionResult BackgroundStyles() => Ok(ApiResponse<IReadOnlyList<PosterBackgroundStyleDto>>.Ok(svc.BackgroundStyles()));

    [HttpPost("background")]
    public async Task<IActionResult> GenerateBackground(GenerateBackgroundRequest req, CancellationToken ct)
        => Ok(ApiResponse<PosterBackgroundResult>.Ok(await svc.GenerateBackgroundAsync(req.Poster, req.Style, UserId, ct), "Background generated."));

    [HttpPost]
    public async Task<IActionResult> Create(PosterStudioRequest req, CancellationToken ct)
        => Ok(ApiResponse<PosterCreatedResult>.Ok(await svc.CreateAsync(req, UserId, ct), "Poster created."));
}
