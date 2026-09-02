using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>
/// Video reel planning (MS3·b) — Product → Goal → Platform → a ready-to-voice, ready-to-render scene
/// plan (AI narration + deterministic scenes/captions). Admin + feature-gated; part of the Marketing
/// Studio module. The plan is voiced via the Voice endpoint and rendered to MP4 by the render worker
/// (next slice).
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[RequiresFeature("marketing_studio")]
[Route("api/marketing/video")]
public sealed class MarketingVideoController(IVideoPlanService svc, IReelRenderService render) : ControllerBase
{
    private long? UserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct)
        => Ok(ApiResponse<VideoOptionsDto>.Ok(await svc.OptionsAsync(ct)));

    [HttpPost("plan")]
    public async Task<IActionResult> Plan(VideoPlanRequest req, CancellationToken ct)
        => Ok(ApiResponse<VideoPlan>.Ok(await svc.BuildAsync(req, UserId, ct), "Here's your reel plan."));

    [HttpPost("render")]
    public async Task<IActionResult> Render(RenderReelRequest req, CancellationToken ct)
        => Ok(ApiResponse<object>.Ok(new { jobId = await render.EnqueueAsync(req, UserId, ct) }, "Your reel is rendering — this takes a minute."));

    [HttpGet("render/{jobId:long}")]
    public async Task<IActionResult> RenderStatus(long jobId, CancellationToken ct)
    {
        var status = await render.StatusAsync(jobId, ct);
        return status is null ? NotFound(ApiResponse<object>.Fail("Not found.")) : Ok(ApiResponse<RenderStatusDto>.Ok(status));
    }
}
