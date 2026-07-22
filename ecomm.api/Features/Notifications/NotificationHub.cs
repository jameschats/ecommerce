using System.Security.Claims;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Notifications;

/// <summary>
/// Real-time push for the notification bell and live conversation delivery. On connect, each client
/// joins its own <c>user-{id}</c> group and admins additionally join the <c>admins</c> group.
///
/// Clients may also join a <c>conversation-{id}</c> group, but only after the server checks they are
/// a party to it — a group name is not a secret, so membership must never be taken on trust.
/// </summary>
[Authorize]
public sealed class NotificationHub(EcommerceDbContext db) : Hub
{
    public const string AdminsGroup = "admins";
    public static string UserGroup(long userId) => $"user-{userId}";
    public static string ConversationGroup(long conversationId) => $"conversation-{conversationId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;   // NameIdentifier claim
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        if (Context.User?.IsInRole("Admin") == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);
        await base.OnConnectedAsync();
    }

    /// <summary>Subscribe to live messages on a conversation the caller is a party to.</summary>
    public async Task JoinConversation(long conversationId)
    {
        if (!await CanAccessAsync(conversationId))
            throw new HubException("Conversation not found.");
        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
    }

    public Task LeaveConversation(long conversationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));

    /// <summary>
    /// Who may listen to a conversation. Read with <c>IgnoreQueryFilters</c> and the tenant compared
    /// explicitly: the ambient tenant scope belongs to the original connection request, which is not a
    /// safe thing to lean on inside a long-lived socket.
    /// </summary>
    private async Task<bool> CanAccessAsync(long conversationId)
    {
        if (!long.TryParse(Context.UserIdentifier, out var userId)) return false;

        var convo = await db.SupportTickets.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.SupportTicketId == conversationId)
            .Select(c => new { c.TenantId, c.Axis, c.ShopperUserId })
            .FirstOrDefaultAsync();
        if (convo is null) return false;

        var user = Context.User;
        var isSuperAdmin = user?.IsInRole("SuperAdmin") == true;
        var isAdmin = user?.IsInRole("Admin") == true;
        var callerTenant = long.TryParse(user?.FindFirstValue("tenant"), out var t) ? t : 0;
        var isTenantStaff = isAdmin && callerTenant == convo.TenantId;

        return convo.Axis switch
        {
            // The platform sees every tenant's tickets; a merchant sees only their own.
            ConversationAxis.MerchantPlatform => isSuperAdmin || isTenantStaff,
            // The shopper who owns it, or the store it was sent to.
            ConversationAxis.ShopperMerchant => isTenantStaff || (convo.ShopperUserId is { } sid && sid == userId),
            _ => false,
        };
    }
}
