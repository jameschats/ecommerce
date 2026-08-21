using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Notifications;

/// <summary>
/// "One engine, many channels": for a notification type (template <c>Code</c>), tries channels
/// in a hardcoded v1 priority order until one succeeds, falling back on failure — not sending to
/// every channel at once. A channel absent from DI (e.g. WhatsApp before Track C ships) or unable
/// to reach the recipient (<see cref="INotificationChannel.CanDeliverTo"/>) is silently skipped,
/// so wiring up a new channel later needs no change here beyond registering it.
///
/// <see cref="NotificationService"/> keeps its existing single-channel SendEmailAsync/SendSmsAsync
/// API on top of this (a recipient with only Email set naturally skips every non-Email channel in
/// the chain) — today's call sites are unchanged and see identical behavior. Genuine multi-channel
/// fallback (a recipient with both Email and Phone, one chain, one winner) is available to any
/// caller that injects this router directly instead of <see cref="INotificationService"/>.
/// </summary>
public interface INotificationRouter
{
    Task<bool> DispatchAsync(string code, NotificationRecipient recipient,
        IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default);

    /// <summary>Hangfire-invoked only — the single scheduled retry after every channel in the
    /// chain failed on the first attempt. Deliberately does not itself schedule another retry, so
    /// a permanently undeliverable recipient stops after two total attempts, not forever.</summary>
    Task RetryOnceAsync(string code, NotificationRecipient recipient,
        Dictionary<string, string> tokens, CancellationToken ct = default);
}

/// <summary>Schedules the router's single delayed retry. Wraps Hangfire's static <c>BackgroundJob</c>
/// API behind an interface purely for testability — <c>BackgroundJob.Schedule</c> throws unless a
/// real <c>JobStorage</c> is configured, which unit tests don't set up.</summary>
public interface IBackgroundJobScheduler
{
    void ScheduleNotificationRetry(string code, NotificationRecipient recipient, Dictionary<string, string> tokens, TimeSpan delay);
}

public sealed class HangfireBackgroundJobScheduler : IBackgroundJobScheduler
{
    public void ScheduleNotificationRetry(string code, NotificationRecipient recipient, Dictionary<string, string> tokens, TimeSpan delay)
        => BackgroundJob.Schedule<INotificationRouter>(r => r.RetryOnceAsync(code, recipient, tokens, CancellationToken.None), delay);
}

public sealed class NotificationRouter : INotificationRouter
{
    /// <summary>v1 hardcoded routing table (per Track A's design decision — not a per-tenant admin
    /// UI yet, there's no delivery data to justify tuning it per merchant). WhatsApp is listed
    /// ahead of SMS/Email for order updates per the design doc even though no WhatsApp channel is
    /// registered yet; the router just skips it until Track C ships.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> ChannelChains =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderConfirmation"] = ["Email", "SMS"],
            ["OrderStatusUpdate"] = ["WhatsApp", "SMS", "Email"],
            ["OrderShipped"] = ["WhatsApp", "SMS", "Email"],
            ["OrderCancelled"] = ["Email", "SMS"],
            ["PasswordReset"] = ["Email"],
            ["EmailVerification"] = ["Email"],
            ["ConversationReply"] = ["Email"],
        };
    private static readonly string[] DefaultChain = ["Email", "SMS"];
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;
    private readonly IReadOnlyDictionary<string, INotificationChannel> _channels;
    private readonly IBackgroundJobScheduler _scheduler;
    private readonly ILogger<NotificationRouter> _logger;

    public NotificationRouter(EcommerceDbContext db, IEnumerable<INotificationChannel> channels,
        IBackgroundJobScheduler scheduler, ILogger<NotificationRouter> logger)
    {
        _db = db;
        _channels = channels.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
        _scheduler = scheduler;
        _logger = logger;
    }

    public async Task<bool> DispatchAsync(string code, NotificationRecipient recipient,
        IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default)
    {
        var sent = await DispatchCoreAsync(code, recipient, tokens, ct);
        if (!sent)
        {
            _logger.LogWarning("Notification {Code} unsent after trying its full chain — scheduling one retry in {Delay}.", code, RetryDelay);
            _scheduler.ScheduleNotificationRetry(code, recipient, new Dictionary<string, string>(tokens), RetryDelay);
        }
        return sent;
    }

    public Task RetryOnceAsync(string code, NotificationRecipient recipient, Dictionary<string, string> tokens, CancellationToken ct = default)
        => DispatchCoreAsync(code, recipient, tokens, ct);

    private async Task<bool> DispatchCoreAsync(string code, NotificationRecipient recipient,
        IReadOnlyDictionary<string, string> tokens, CancellationToken ct)
    {
        var chain = ChannelChains.GetValueOrDefault(code, DefaultChain);
        var groupId = Guid.NewGuid().ToString();
        var attempt = 0;

        foreach (var channelKey in chain)
        {
            if (!_channels.TryGetValue(channelKey, out var channel)) continue;
            if (!channel.CanDeliverTo(recipient)) continue;

            attempt++;
            if (await TryChannelAsync(channel, code, channelKey, recipient, tokens, groupId, attempt, ct))
                return true;
        }

        if (attempt == 0)
            _logger.LogWarning("Notification {Code} not dispatched — no channel in its chain could reach recipient {UserId}.", code, recipient.UserId);
        return false;
    }

    private async Task<bool> TryChannelAsync(INotificationChannel channel, string code, string channelKey,
        NotificationRecipient recipient, IReadOnlyDictionary<string, string> tokens, string groupId, int attempt, CancellationToken ct)
    {
        var template = await _db.NotificationTemplates.AsNoTracking().FirstOrDefaultAsync(
            t => t.TenantId == Tenant && t.Code == code && t.Channel == channelKey && t.IsActive, ct);
        if (template is null)
        {
            _logger.LogWarning("No active {Channel} template '{Code}' — skipping to next channel in chain.", channelKey, code);
            return false;
        }

        var subject = Render(template.Subject, tokens);
        var body = Render(template.Body, tokens) ?? "";
        var recipientAddress = channelKey.Equals("Email", StringComparison.OrdinalIgnoreCase) ? recipient.Email : recipient.Phone;

        var history = new NotificationHistory
        {
            TenantId = Tenant,
            TemplateId = template.NotificationTemplateId,
            Channel = channelKey,
            Recipient = recipientAddress ?? "",
            Subject = subject,
            Body = body,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            AttemptGroupId = groupId,
            AttemptNumber = attempt,
        };
        _db.NotificationHistory.Add(history);

        var ok = false;
        try
        {
            var metadata = channelKey.Equals("Email", StringComparison.OrdinalIgnoreCase)
                ? await EmailMetadataAsync(ct) : null;
            ok = await channel.SendAsync(recipient, subject ?? "", body, metadata, ct);
            history.Status = ok ? "Sent" : "Failed";
            if (ok) history.SentAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            history.Status = "Failed";
            history.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            _logger.LogError(ex, "Failed to send {Channel} '{Code}' to {Recipient}.", channelKey, code, recipientAddress);
        }

        await _db.SaveChangesAsync(ct);
        return ok;
    }

    /// <summary>Per-tenant email sender identity (display name + reply-to) from merchant Settings.</summary>
    private async Task<IReadOnlyDictionary<string, string>?> EmailMetadataAsync(CancellationToken ct)
    {
        var rows = await _db.Settings.AsNoTracking()
            .Where(s => s.TenantId == Tenant && (s.SettingKey == "SenderName" || s.SettingKey == "ReplyToEmail"))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);
        var metadata = new Dictionary<string, string>();
        if (rows.TryGetValue("SenderName", out var fromName) && !string.IsNullOrWhiteSpace(fromName)) metadata["FromName"] = fromName;
        if (rows.TryGetValue("ReplyToEmail", out var replyTo) && !string.IsNullOrWhiteSpace(replyTo)) metadata["ReplyTo"] = replyTo;
        return metadata.Count > 0 ? metadata : null;
    }

    /// <summary>Replaces <c>{{key}}</c> placeholders (case-insensitive, optional surrounding spaces).</summary>
    private static string? Render(string? template, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(template)) return template;
        var result = template;
        foreach (var (key, value) in tokens)
            result = System.Text.RegularExpressions.Regex.Replace(
                result, "{{\\s*" + System.Text.RegularExpressions.Regex.Escape(key) + "\\s*}}",
                value ?? "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return result;
    }
}
