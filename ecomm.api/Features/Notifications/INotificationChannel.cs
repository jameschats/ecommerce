using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.WhatsApp;

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

/// <summary>Adapts <see cref="IWhatsAppProvider"/> to the router. Unlike Email/SMS, WhatsApp needs
/// two extra pieces the router computes and passes via <paramref name="metadata"/> below (since
/// Meta requires referencing a pre-approved template by id + ordered params, not sending rendered
/// text directly): "ExternalTemplateId" and "TemplateParams" (parameter values joined by U+001F,
/// in the order their <c>{{token}}</c> placeholders appeared in the template body).</summary>
public sealed class WhatsAppNotificationChannel : INotificationChannel
{
    /// <summary>Unit Separator — safe for joining template parameter values since it can't appear
    /// in ordinary rendered token text. Also used by <see cref="NotificationRouter"/> when building
    /// the "TemplateParams" metadata value.</summary>
    public const char TemplateParamDelimiter = (char)31;

    private readonly IWhatsAppProvider _provider;
    private readonly ILogger<WhatsAppNotificationChannel> _logger;

    public WhatsAppNotificationChannel(IWhatsAppProvider provider, ILogger<WhatsAppNotificationChannel> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public string Key => "WhatsApp";

    public bool CanDeliverTo(NotificationRecipient recipient) => !string.IsNullOrWhiteSpace(recipient.Phone);

    public async Task<bool> SendAsync(NotificationRecipient recipient, string subject, string body,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken ct = default)
    {
        var templateId = metadata?.GetValueOrDefault("ExternalTemplateId");
        if (string.IsNullOrWhiteSpace(templateId))
        {
            _logger.LogWarning("WhatsApp template for this notification has no ExternalTemplateId configured — cannot send.");
            return false;
        }
        var parameters = metadata!.GetValueOrDefault("TemplateParams")?.Split(TemplateParamDelimiter) ?? [];

        var result = await _provider.SendTemplateMessageAsync(recipient.Phone!, templateId, parameters, ct);
        if (!result.Success) _logger.LogError("WhatsApp channel failed to send to {Phone}: {Error}", recipient.Phone, result.Error);
        return result.Success;
    }
}
