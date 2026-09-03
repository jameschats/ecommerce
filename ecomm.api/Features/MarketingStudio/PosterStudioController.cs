using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.MarketingStudio;

public sealed record SuggestHeadlineRequest(long? ProductId, string? Topic);
public sealed record GenerateBackgroundRequest(PosterStudioRequest Poster, string Style);
public sealed record AutoFillDraftRequest(string Kind, long? ProductId);
public sealed record CreatePosterDocumentRequest(PosterDocument Document, long MediaFileId, string? Caption = null);
public sealed record UpdatePosterDocumentRequest(PosterDocument Document, long MediaFileId, string Caption);

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

    // Canvas-editor create/update — takes over the primary POST/PUT routes going forward. The old
    // flat-field CreateAsync/UpdateAsync(PosterStudioRequest, ...) stay in the service (still exercised
    // by tests, still what GetAsync/DuplicateAsync read for posters made before this existed) but are
    // no longer reachable via HTTP: a legacy poster is view-only + duplicable from here on, never
    // re-saved through the old form.
    [HttpPost]
    public async Task<IActionResult> Create(CreatePosterDocumentRequest req, CancellationToken ct)
        => Ok(ApiResponse<PosterCreatedResult>.Ok(await svc.CreateFromDocumentAsync(req.Document, req.MediaFileId, req.Caption, UserId, ct), "Poster created."));

    [HttpPut("{creativeId:long}")]
    public async Task<IActionResult> Update(long creativeId, UpdatePosterDocumentRequest req, CancellationToken ct)
        => Ok(ApiResponse<PosterCreatedResult>.Ok(await svc.UpdateFromDocumentAsync(creativeId, req.Document, req.MediaFileId, req.Caption, UserId, ct), "Poster updated."));

    [HttpGet("{creativeId:long}")]
    public async Task<IActionResult> Get(long creativeId, CancellationToken ct)
        => Ok(ApiResponse<PosterDetailDto>.Ok(await svc.GetAsync(creativeId, ct)));

    [HttpPost("{creativeId:long}/duplicate")]
    public async Task<IActionResult> Duplicate(long creativeId, CancellationToken ct)
        => Ok(ApiResponse<PosterCreatedResult>.Ok(await svc.DuplicateAsync(creativeId, ct), "Poster duplicated."));

    [HttpPost("auto-fill")]
    public async Task<IActionResult> AutoFillDraft(AutoFillDraftRequest req, CancellationToken ct)
        => Ok(ApiResponse<AutoFillDraftResult>.Ok(await svc.AutoFillDraftAsync(req.Kind, req.ProductId, ct)));

    [HttpGet("templates/{templateId}/document")]
    public async Task<IActionResult> TemplateDocument(string templateId, [FromQuery] string? format, CancellationToken ct)
        => Ok(ApiResponse<PosterDocument>.Ok(await svc.TemplateDocumentAsync(templateId, format ?? "square", ct)));
}
