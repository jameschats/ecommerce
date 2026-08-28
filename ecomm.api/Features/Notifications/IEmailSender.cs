using System.Net;
using System.Net.Mail;

namespace ecomm.api.Features.Notifications;

/// <summary>
/// Sends an email. Two implementations, selected by <c>Email:Provider</c>:
/// <see cref="LoggingEmailSender"/> (dev — logs) and <see cref="SmtpEmailSender"/> (real).
/// Swapping to a hosted provider later means adding one class — callers don't change.
/// </summary>
public interface IEmailSender
{
    /// <param name="fromName">Optional per-tenant sender display name (envelope address stays the platform address for SPF/DKIM).</param>
    /// <param name="replyTo">Optional per-tenant reply-to address, so replies reach the merchant.</param>
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default,
        string? fromName = null, string? replyTo = null);
}

/// <summary>Dev/stub email sender — logs the message instead of sending it.</summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default,
        string? fromName = null, string? replyTo = null)
    {
        _logger.LogWarning("[DEV EMAIL] From {From} (reply-to {ReplyTo}) To {To} | Subject: {Subject}\n{Body}",
            fromName ?? "(default)", replyTo ?? "(none)", toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}

/// <summary>Real SMTP sender (used when the resolved provider is <c>Smtp</c> and credentials are set).
/// Constructed with already-resolved <see cref="EmailOptions"/> — <see cref="EmailSenderFactory"/> builds
/// it per request from either the platform DB row or the env fallback, so config changes take effect
/// without a restart.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _opts;

    public SmtpEmailSender(EmailOptions opts) => _opts = opts;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default,
        string? fromName = null, string? replyTo = null)
    {
        using var message = new MailMessage
        {
            // Envelope address stays the authenticated platform address (SPF/DKIM); only the
            // display name is overridden per tenant. Replies are routed to the merchant via Reply-To.
            From = new MailAddress(_opts.FromAddress, string.IsNullOrWhiteSpace(fromName) ? _opts.FromName : fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(toEmail);
        if (!string.IsNullOrWhiteSpace(replyTo)) message.ReplyToList.Add(new MailAddress(replyTo));

        using var client = new SmtpClient(_opts.Host, _opts.Port)
        {
            EnableSsl = _opts.UseSsl,
            Credentials = new NetworkCredential(_opts.Username, _opts.Password),
        };
        await client.SendMailAsync(message, ct);
    }
}
