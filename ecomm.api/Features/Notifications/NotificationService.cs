using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Services;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Notifications;

/// <summary>
/// Sends templated notifications and records every attempt in <c>NotificationHistory</c>.
/// Message content comes from admin-editable <c>NotificationTemplates</c> (by Code + Channel),
/// with <c>{{token}}</c> placeholders substituted per call. Sending never throws to the caller —
/// a failed email must not break order placement; it's logged + recorded as Failed.
/// </summary>
public interface INotificationService
{
    Task<bool> SendEmailAsync(string code, string toEmail, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default);
    Task<bool> SendSmsAsync(string code, string toPhone, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default);
}

public sealed class NotificationService : INotificationService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(EcommerceDbContext db, IEmailSender email, ISmsSender sms, ILogger<NotificationService> logger)
    {
        _db = db;
        _email = email;
        _sms = sms;
        _logger = logger;
    }

    public Task<bool> SendEmailAsync(string code, string toEmail, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default)
        => SendAsync(code, "Email", toEmail, tokens, ct);

    public Task<bool> SendSmsAsync(string code, string toPhone, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default)
        => SendAsync(code, "SMS", toPhone, tokens, ct);

    private async Task<bool> SendAsync(string code, string channel, string recipient, IReadOnlyDictionary<string, string> tokens, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            _logger.LogWarning("Notification {Code}/{Channel} skipped — no recipient.", code, channel);
            return false;
        }

        var template = await _db.NotificationTemplates.AsNoTracking().FirstOrDefaultAsync(
            t => t.TenantId == Tenant && t.Code == code && t.Channel == channel && t.IsActive, ct);
        if (template is null)
        {
            _logger.LogWarning("No active {Channel} template '{Code}' — notification not sent.", channel, code);
            return false;
        }

        var subject = Render(template.Subject, tokens);
        var body = Render(template.Body, tokens) ?? "";

        var history = new NotificationHistory
        {
            TenantId = Tenant,
            TemplateId = template.NotificationTemplateId,
            Channel = channel,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
        };
        _db.NotificationHistory.Add(history);

        try
        {
            if (channel == "Email")
            {
                var (fromName, replyTo) = await SenderIdentityAsync(ct);
                await _email.SendAsync(recipient, subject ?? "", body, ct, fromName, replyTo);
            }
            else
                await _sms.SendAsync(recipient, body, ct);

            history.Status = "Sent";
            history.SentAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            history.Status = "Failed";
            history.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            _logger.LogError(ex, "Failed to send {Channel} '{Code}' to {Recipient}.", channel, code, recipient);
        }

        await _db.SaveChangesAsync(ct);
        return history.Status == "Sent";
    }

    /// <summary>Per-tenant email sender identity (display name + reply-to) from merchant Settings.</summary>
    private async Task<(string? fromName, string? replyTo)> SenderIdentityAsync(CancellationToken ct)
    {
        var rows = await _db.Settings.AsNoTracking()
            .Where(s => s.TenantId == Tenant && (s.SettingKey == "SenderName" || s.SettingKey == "ReplyToEmail"))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);
        return (rows.GetValueOrDefault("SenderName"), rows.GetValueOrDefault("ReplyToEmail"));
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
