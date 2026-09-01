using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>
/// Voiceover generation (MS3·a) — the first slice of the video pipeline. Lists supported
/// languages/voices and synthesises a preview MP3 via Sarvam TTS. Admin + feature-gated; part of the
/// Marketing Studio module (/api/marketing/*). The reel assembly (render worker) comes next.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[RequiresFeature("marketing_studio")]
[Route("api/marketing/voice")]
public sealed class MarketingVoiceController(IMarketingVoiceService svc) : ControllerBase
{
    [HttpGet("options")]
    public IActionResult Options() => Ok(ApiResponse<VoiceOptionsDto>.Ok(svc.Options()));

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(VoicePreviewRequest req, CancellationToken ct)
        => Ok(ApiResponse<VoicePreviewResult>.Ok(await svc.PreviewAsync(req, ct), "Voiceover ready."));
}
