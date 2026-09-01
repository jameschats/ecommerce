using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>
/// Social connections for the Marketing Studio (MS1). The list/start/disconnect endpoints are
/// merchant-admin + feature-gated; the OAuth <c>callback</c> is anonymous (the provider redirects the
/// browser to it) and attributes the connection via the signed <c>state</c>, then bounces the merchant
/// back to the connections page. Part of the Marketing Studio module (/api/marketing/*, §3.10 seam).
/// </summary>
[ApiController]
[Route("api/marketing/connections")]
public sealed class MarketingConnectionsController(ISocialConnectionService svc, IOptions<SocialOptions> opt) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [RequiresFeature("marketing_studio")]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<SocialConnectionDto>>.Ok(await svc.ListAsync(ct)));

    [HttpPost("{platform}/start")]
    [Authorize(Roles = "Admin")]
    [RequiresFeature("marketing_studio")]
    public async Task<IActionResult> Start(string platform, CancellationToken ct)
        => Ok(ApiResponse<StartConnectResult>.Ok(await svc.StartAsync(platform, ct)));

    [HttpDelete("{platform}")]
    [Authorize(Roles = "Admin")]
    [RequiresFeature("marketing_studio")]
    public async Task<IActionResult> Disconnect(string platform, CancellationToken ct)
    {
        await svc.DisconnectAsync(platform, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Disconnected."));
    }

    /// <summary>OAuth redirect target. Anonymous — trust comes from the signed <c>state</c>, not a session.
    /// Completes the exchange, then 302s the merchant back to the connections page with a status flag.</summary>
    [HttpGet("{platform}/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(string platform, [FromQuery] string? code, [FromQuery] string? state,
        [FromQuery] string? error, CancellationToken ct)
    {
        var back = $"{opt.Value.RedirectBaseUrl.TrimEnd('/')}/admin/marketing/connections";
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect($"{back}?error={Uri.EscapeDataString(error ?? "cancelled")}");

        try
        {
            await svc.CompleteAsync(platform, code!, state!, ct);
            return Redirect($"{back}?connected={Uri.EscapeDataString(platform)}");
        }
        catch (Exception)
        {
            return Redirect($"{back}?error={Uri.EscapeDataString(platform)}");
        }
    }
}
