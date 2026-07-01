using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Notifications;

/// <summary>
/// Sends an email. Two implementations, selected by <c>Email:Provider</c>:
/// <see cref="LoggingEmailSender"/> (dev — logs) and <see cref="SmtpEmailSender"/> (real).
/// Swapping to a hosted provider later means adding one class — callers don't change.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>Dev/stub email sender — logs the message instead of sending it.</summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogWarning("[DEV EMAIL] To {To} | Subject: {Subject}\n{Body}", toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}

/// <summary>Real SMTP sender (used when <c>Email:Provider=Smtp</c> and credentials are set).</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _opts;

    public SmtpEmailSender(IOptions<EmailOptions> opts) => _opts = opts.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_opts.FromAddress, _opts.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_opts.Host, _opts.Port)
        {
            EnableSsl = _opts.UseSsl,
            Credentials = new NetworkCredential(_opts.Username, _opts.Password),
        };
        await client.SendMailAsync(message, ct);
    }
}
