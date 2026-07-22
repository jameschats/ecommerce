using Microsoft.AspNetCore.SignalR;

namespace ecomm.api.Features.Notifications;

/// <summary>A message pushed live to everyone watching a conversation.</summary>
public sealed record LiveMessageDto(long ConversationId, long MessageId, string AuthorType, string Body, DateTime CreatedAt);

public interface IConversationRealtime
{
    Task MessageAsync(LiveMessageDto message, CancellationToken ct = default);
}

/// <summary>
/// Pushes conversation messages to the <c>conversation-{id}</c> group.
///
/// Callers must persist first and broadcast second. The socket is an accelerator, not the record:
/// a dropped connection, a closed tab or a second server instance must never be able to lose a
/// message, and a reconnecting client re-reads the thread rather than replaying pushes.
///
/// Delivery failures are swallowed — a websocket problem must not roll back a saved reply.
/// </summary>
public sealed class ConversationRealtime(IHubContext<NotificationHub> hub, ILogger<ConversationRealtime> log)
    : IConversationRealtime
{
    public async Task MessageAsync(LiveMessageDto message, CancellationToken ct = default)
    {
        try
        {
            await hub.Clients.Group(NotificationHub.ConversationGroup(message.ConversationId))
                .SendAsync("conversationMessage", message, ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Live push failed for conversation {Id} — the message is saved regardless.", message.ConversationId);
        }
    }
}
