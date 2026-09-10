using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.GoLive;

[ApiController]
[Route("api/admin/go-live")]
[Authorize(Roles = "Admin")]
public sealed class GoLiveController : ControllerBase
{
    private readonly IGoLiveService _goLive;

    public GoLiveController(IGoLiveService goLive) => _goLive = goLive;

    private long? CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
        => Ok(ApiResponse<GoLiveSummaryDto>.Ok(await _goLive.GetSummaryAsync(ct)));

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(GoLiveResetRequest request, CancellationToken ct)
        => Ok(ApiResponse<GoLiveResetResultDto>.Ok(await _goLive.ResetAsync(request, CurrentUserId, ct), "Trading records cleared."));

    [HttpPost("mark-live")]
    public async Task<IActionResult> MarkLive(CancellationToken ct)
    {
        await _goLive.MarkLiveAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }, "This shop is now live."));
    }
}
