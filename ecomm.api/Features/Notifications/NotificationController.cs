using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Notifications;

/// <summary>The notification bell: the current user's (or, for admins, the admin) feed.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationController : ControllerBase
{
    private readonly INotificationFeedService _feed;
    public NotificationController(INotificationFeedService feed) => _feed = feed;

    private long CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;
    private bool IsAdmin => User.IsInRole("Admin");

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 15, CancellationToken ct = default)
        => Ok(ApiResponse<List<NotificationDto>>.Ok(await _feed.ListAsync(CurrentUserId, IsAdmin, limit, ct)));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
        => Ok(ApiResponse<int>.Ok(await _feed.UnreadCountAsync(CurrentUserId, IsAdmin, ct)));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        await _feed.MarkReadAsync(CurrentUserId, IsAdmin, id, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Marked read."));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _feed.MarkAllReadAsync(CurrentUserId, IsAdmin, ct);
        return Ok(ApiResponse<object>.Ok(null!, "All marked read."));
    }
}
