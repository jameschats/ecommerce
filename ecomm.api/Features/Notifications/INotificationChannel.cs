using ecomm.api.Features.Auth.Services;

namespace ecomm.api.Features.Notifications;

/// <summary>A recipient's contact points across channels — a channel picks whichever field it
/// needs and reports itself undeliverable (<see cref="INotificationChannel.CanDeliverTo"/>) if
/// its field is missing, so the router can silently skip to the next channel in the chain.</summary>
public sealed record NotificationRecipient(long? UserId = null, string? Email = null, string? Phone = null);

/// <summary>One outbound delivery mechanism (Email, SMS today; WhatsApp/Push once those ship).
/// Deliberately thin — template lookup, rendering, and history live in <see cref="NotificationRouter"/>;
/// a channel only knows how to hand a rendered subject/body to its underlying sender.</summary>
public interface INotificationChannel
{
    /// <summary>Matches the <c>NotificationTemplates.Channel</c> / <c>NotificationHistory.Channel</c> value, e.g. "Email".</summary>
    string Key { get; }

    bool CanDeliverTo(NotificationRecipient recipient);

    /// <param name="metadata">Channel-specific extras the router resolved (e.g. Email's per-tenant
    /// FromName/ReplyTo). Channels that don't need any simply ignore it.</param>
    Task<bool> SendAsync(NotificationRecipient recipient, string subject, string body,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken ct = default);
}

public sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly IEmailSender _email;
    private readonly ILogger<EmailNotificationChannel> _logger;

    public EmailNotificationChannel(IEmailSender email, ILogger<EmailNotificationChannel> logger)
    {
        _email = email;
        _logger = logger;
    }

    public string Key => "Email";

    public bool CanDeliverTo(NotificationRecipient recipient) => !string.IsNullOrWhiteSpace(recipient.Email);

    public async Task<bool> SendAsync(NotificationRecipient recipient, string subject, string body,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken ct = default)
    {
        try
        {
            var fromName = metadata?.GetValueOrDefault("FromName");
            var replyTo = metadata?.GetValueOrDefault("ReplyTo");
            await _email.SendAsync(recipient.Email!, subject, body, ct, fromName, replyTo);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email channel failed to send to {Email}.", recipient.Email);
            return false;
        }
    }
}

public sealed class SmsNotificationChannel : INotificationChannel
{
    private readonly ISmsSender _sms;
    private readonly ILogger<SmsNotificationChannel> _logger;

    public SmsNotificationChannel(ISmsSender sms, ILogger<SmsNotificationChannel> logger)
    {
        _sms = sms;
        _logger = logger;
    }

    public string Key => "SMS";

    public bool CanDeliverTo(NotificationRecipient recipient) => !string.IsNullOrWhiteSpace(recipient.Phone);

    public async Task<bool> SendAsync(NotificationRecipient recipient, string subject, string body,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken ct = default)
    {
        try
        {
            await _sms.SendAsync(recipient.Phone!, body, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMS channel failed to send to {Phone}.", recipient.Phone);
            return false;
        }
    }
}
