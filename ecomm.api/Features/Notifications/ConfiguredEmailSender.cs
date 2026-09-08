using System.Net;
using System.Net.Mail;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Notifications;

/// <summary>
/// Email sender that resolves its mode and SMTP settings from the database on every send
/// (design.md §9.2).
///
/// The previous wiring picked Logging or Smtp once at startup from appsettings.json, so
/// going Live meant editing a file on the server and restarting. Reading per send means an
/// admin flips it from a screen and the next email obeys — which is the whole point of
/// having a Mock/Live switch at all.
///
/// Mock and Live differ only in the final step: in Mock the rendered message is logged and
/// not handed to Brevo. Everything upstream — templates, recipients, history — is identical.
/// </summary>
public sealed class ConfiguredEmailSender : IEmailSender
{
    private readonly EcommerceDbContext _db;
    private readonly ILogger<ConfiguredEmailSender> _logger;

    public ConfiguredEmailSender(EcommerceDbContext db, ILogger<ConfiguredEmailSender> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var cfg = await LoadAsync(ct);

        if (!IsLive(cfg))
        {
            // Warning, not Information: a mocked channel in a running environment is
            // something an operator should notice rather than have to go looking for.
            _logger.LogWarning("[MOCK EMAIL] To {To} | {Subject}\n{Body}", toEmail, subject, htmlBody);
            return;
        }

        var host = Get(cfg, "Email.SmtpHost");
        var from = Get(cfg, "Email.FromAddress");
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            // Live but unconfigured. Log loudly and do not throw: a half-set-up mail server
            // should not take down order placement, which is the caller here.
            _logger.LogError(
                "Email mode is Live but SMTP host or from-address is not set. Message to {To} was not sent.", toEmail);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(from, Get(cfg, "Email.FromName") is { Length: > 0 } n ? n : "DailyCalendarShop"),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        // Admin-entered lists can carry a stray trailing comma or extra whitespace (the
        // recipients field is free text); a blank entry there would otherwise throw here
        // and silently drop every admin alert rather than just the malformed one.
        foreach (var address in toEmail.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            message.To.Add(address);

        using var client = new SmtpClient(host, PortOf(cfg))
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(Get(cfg, "Email.SmtpUsername"), Get(cfg, "Email.SmtpPassword")),
        };

        await client.SendMailAsync(message, ct);
        _logger.LogInformation("Email sent to {To} — {Subject}", toEmail, subject);
    }

    /// <summary>
    /// Sends a test message and lets failures surface, unlike <see cref="SendAsync"/> which
    /// deliberately swallows them. The whole value of a "send test" button is the error.
    /// </summary>
    public async Task SendTestAsync(string toEmail, CancellationToken ct = default)
    {
        var cfg = await LoadAsync(ct);

        if (!IsLive(cfg))
            throw new AppException("Email is in Mock mode. Switch it to Live to send a real test message.");
        if (string.IsNullOrWhiteSpace(Get(cfg, "Email.SmtpHost")))
            throw new AppException("SMTP host is not set.");
        if (string.IsNullOrWhiteSpace(Get(cfg, "Email.FromAddress")))
            throw new AppException("From address is not set. It must be a sender you have verified with Brevo.");

        try
        {
            await SendAsync(
                toEmail,
                "DailyCalendarShop — test email",
                "<p>This is a test message from your DailyCalendarShop admin settings.</p>"
                + "<p>If you are reading this, SMTP is configured correctly.</p>",
                ct);
        }
        catch (SmtpException ex)
        {
            // Brevo's most common rejections are an unverified sender and a bad SMTP key.
            // Say so, rather than surfacing a bare SMTP status code.
            throw new AppException(
                $"SMTP rejected the message: {ex.StatusCode}. "
                + "Check the SMTP key is correct and that the from-address is a verified sender in Brevo.");
        }
    }

    private async Task<Dictionary<string, string?>> LoadAsync(CancellationToken ct)
        => await _db.Settings
            .Where(s => s.SettingKey.StartsWith("Email.") || s.SettingKey == "Channels.EmailMode")
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);

    /// <summary>Anything other than an explicit "Live" is Mock — an unresolvable mode must
    /// never default to attempting real delivery.</summary>
    private static bool IsLive(Dictionary<string, string?> cfg)
        => cfg.TryGetValue("Channels.EmailMode", out var mode)
           && string.Equals(mode, "Live", StringComparison.OrdinalIgnoreCase);

    private static string Get(Dictionary<string, string?> cfg, string key)
        => cfg.TryGetValue(key, out var v) ? v ?? "" : "";

    private static int PortOf(Dictionary<string, string?> cfg)
        => int.TryParse(Get(cfg, "Email.SmtpPort"), out var p) && p > 0 ? p : 587;
}
