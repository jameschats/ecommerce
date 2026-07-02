using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ecomm.api.Features.Notifications;

/// <summary>
/// Real-time push for the notification bell. On connect, each client joins its own
/// <c>user-{id}</c> group and admins additionally join the <c>admins</c> group;
/// the feed service pushes new notifications to the relevant group.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public const string AdminsGroup = "admins";
    public static string UserGroup(long userId) => $"user-{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;   // NameIdentifier claim
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        if (Context.User?.IsInRole("Admin") == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);
        await base.OnConnectedAsync();
    }
}
