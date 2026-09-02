using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.MarketingStudio;

public sealed record SuggestHeadlineRequest(long? ProductId, string? Topic);
public sealed record GenerateBackgroundRequest(PosterStudioRequest Poster, string Style);
public sealed record UpdatePosterRequest(PosterStudioRequest Poster, string Caption);
public sealed record AutoFillDraftRequest(string Kind, long? ProductId);

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

    [HttpGet("options")]
    public IActionResult Options() => Ok(ApiResponse<PosterEditorOptionsDto>.Ok(svc.Options()));

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

    [HttpGet("{creativeId:long}")]
    public async Task<IActionResult> Get(long creativeId, CancellationToken ct)
        => Ok(ApiResponse<PosterDetailDto>.Ok(await svc.GetAsync(creativeId, ct)));

    [HttpPut("{creativeId:long}")]
    public async Task<IActionResult> Update(long creativeId, UpdatePosterRequest req, CancellationToken ct)
        => Ok(ApiResponse<PosterCreatedResult>.Ok(await svc.UpdateAsync(creativeId, req.Poster, req.Caption, UserId, ct), "Poster updated."));

    [HttpPost("{creativeId:long}/duplicate")]
    public async Task<IActionResult> Duplicate(long creativeId, CancellationToken ct)
        => Ok(ApiResponse<PosterCreatedResult>.Ok(await svc.DuplicateAsync(creativeId, ct), "Poster duplicated."));

    [HttpPost("auto-fill")]
    public async Task<IActionResult> AutoFillDraft(AutoFillDraftRequest req, CancellationToken ct)
        => Ok(ApiResponse<AutoFillDraftResult>.Ok(await svc.AutoFillDraftAsync(req.Kind, req.ProductId, ct)));
}
