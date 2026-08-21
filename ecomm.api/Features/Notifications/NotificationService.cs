namespace ecomm.api.Features.Notifications;

/// <summary>
/// Single-channel convenience API over <see cref="INotificationRouter"/> — template lookup,
/// rendering, history, and channel fallback all live in the router now; this just builds a
/// <see cref="NotificationRecipient"/> with one contact point set, so the router's chain for a
/// given code naturally resolves to that one channel (every other channel in the chain reports
/// itself undeliverable and is skipped). Existing call sites and behavior are unchanged.
/// </summary>
public interface INotificationService
{
    Task<bool> SendEmailAsync(string code, string toEmail, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default);
    Task<bool> SendSmsAsync(string code, string toPhone, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default);
}

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRouter _router;

    public NotificationService(INotificationRouter router) => _router = router;

    public Task<bool> SendEmailAsync(string code, string toEmail, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default)
        => _router.DispatchAsync(code, new NotificationRecipient(Email: toEmail), tokens, ct);

    public Task<bool> SendSmsAsync(string code, string toPhone, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default)
        => _router.DispatchAsync(code, new NotificationRecipient(Phone: toPhone), tokens, ct);
}
