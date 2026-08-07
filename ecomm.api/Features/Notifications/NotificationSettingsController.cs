using ecomm.api.Common.Models;
using ecomm.api.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Notifications;

public sealed record SetNotificationSwitchRequest(string Event, string Channel, bool Enabled);

/// <summary>
/// Which automatic messages go out.
///
/// Separate from the message templates screen on purpose: that one is about wording, this one
/// is about whether anything is sent at all. They were previously the same question only for
/// template-driven messages — the order emails were hardcoded and could not be stopped by
/// anything short of a deploy.
/// </summary>
[ApiController]
[Route("api/admin/notification-settings")]
[Authorize(Policy = Perm.SettingsManage)]
public sealed class NotificationSettingsController : ControllerBase
{
    private readonly INotificationPolicy _policy;
    public NotificationSettingsController(INotificationPolicy policy) => _policy = policy;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<NotificationSwitchDto>>.Ok(await _policy.ListAsync(ct)));

    [HttpPut]
    public async Task<IActionResult> Set([FromBody] SetNotificationSwitchRequest req, CancellationToken ct)
    {
        await _policy.SetAsync(req.Event, req.Channel, req.Enabled, ct);
        return Ok(ApiResponse<List<NotificationSwitchDto>>.Ok(
            await _policy.ListAsync(ct), req.Enabled ? "Switched on." : "Switched off."));
    }
}
