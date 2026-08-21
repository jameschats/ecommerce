namespace ecomm.api.Features.WhatsApp;

public sealed record WhatsAppSendResult(bool Success, string? MessageId, string? Error)
{
    public static WhatsAppSendResult Ok(string messageId) => new(true, messageId, null);
    public static WhatsAppSendResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// WhatsApp Business API distinguishes two message kinds with different rules, modeled as two
/// methods so a future session-message caller (Phase 2's chatbot) doesn't force a rework of this
/// interface: <b>template</b> messages are pre-approved by Meta and are the only kind a business
/// can send outside an active conversation (what Notifications needs); <b>session</b> messages are
/// free-form but only deliverable within 24h of the customer's last inbound message.
/// </summary>
public interface IWhatsAppProvider
{
    Task<WhatsAppSendResult> SendTemplateMessageAsync(string toPhone, string templateId,
        IReadOnlyList<string> parameters, CancellationToken ct = default);

    /// <summary>Not used by the Notification Router (Track A/B only send template messages) —
    /// exists now so Phase 2's chatbot doesn't need a second WhatsApp integration.</summary>
    Task<WhatsAppSendResult> SendSessionMessageAsync(string toPhone, string body, CancellationToken ct = default);
}

/// <summary>Dev/stub provider — logs instead of sending. Selected when <c>WhatsApp:Provider</c> is
/// unset or "None", same pattern as Email/SMS's Logging providers.</summary>
public sealed class LoggingWhatsAppProvider : IWhatsAppProvider
{
    private readonly ILogger<LoggingWhatsAppProvider> _logger;

    public LoggingWhatsAppProvider(ILogger<LoggingWhatsAppProvider> logger) => _logger = logger;

    public Task<WhatsAppSendResult> SendTemplateMessageAsync(string toPhone, string templateId,
        IReadOnlyList<string> parameters, CancellationToken ct = default)
    {
        _logger.LogWarning("[DEV WHATSAPP] Template {TemplateId} to {Phone} | Params: {Params}",
            templateId, toPhone, string.Join(", ", parameters));
        return Task.FromResult(WhatsAppSendResult.Ok(Guid.NewGuid().ToString()));
    }

    public Task<WhatsAppSendResult> SendSessionMessageAsync(string toPhone, string body, CancellationToken ct = default)
    {
        _logger.LogWarning("[DEV WHATSAPP] Session message to {Phone}: {Body}", toPhone, body);
        return Task.FromResult(WhatsAppSendResult.Ok(Guid.NewGuid().ToString()));
    }
}
