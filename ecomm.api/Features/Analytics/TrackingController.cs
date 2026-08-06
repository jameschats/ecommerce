using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Analytics;

/// <summary>Public: first-party page-view beacon. Fire-and-forget from the Angular app on every route change.</summary>
[ApiController]
[Route("api/analytics/track")]
public sealed class TrackingController : ControllerBase
{
    private readonly IPageViewTrackingService _tracking;
    public TrackingController(IPageViewTrackingService tracking) => _tracking = tracking;

    [HttpPost]
    public async Task<IActionResult> Track(TrackPageViewRequest req, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        await _tracking.TrackAsync(req, ip, ua, ct);
        return NoContent();
    }
}
