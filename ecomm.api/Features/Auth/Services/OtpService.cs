using System.Security.Cryptography;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Auth.Services;

public interface IOtpService
{
    Task RequestAsync(string identifier, string channel, string purpose, CancellationToken ct = default);
    Task<bool> VerifyAsync(string identifier, string purpose, string code, CancellationToken ct = default);
}

public sealed class OtpService : IOtpService
{
    private const int ExpiryMinutes = 5;
    private const int ResendCooldownSeconds = 30;

    // Email OTP purposes → the notification template used to deliver the code. "Login" is only
    // ever used as the SMS→email fallback below (mobile-OTP login has no Email-channel call site
    // of its own), while ResetPassword/VerifyEmail are already Email-only flows.
    private static readonly Dictionary<string, string> EmailTemplates = new()
    {
        ["ResetPassword"] = "PasswordReset",
        ["VerifyEmail"] = "EmailVerification",
        ["Login"] = "LoginOtp",
    };

    private readonly EcommerceDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ISmsSender _sms;
    private readonly INotificationService _notify;
    private readonly INotificationChannelSettings _channelSettings;
    private readonly ILogger<OtpService> _logger;

    public OtpService(EcommerceDbContext db, IPasswordHasher hasher, ISmsSender sms, INotificationService notify,
        INotificationChannelSettings channelSettings, ILogger<OtpService> logger)
    {
        _db = db;
        _hasher = hasher;
        _sms = sms;
        _notify = notify;
        _channelSettings = channelSettings;
        _logger = logger;
    }

    public async Task RequestAsync(string identifier, string channel, string purpose, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var recent = await _db.OtpVerifications
            .Where(o => o.Identifier == identifier && o.Purpose == purpose && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (recent is not null && recent.CreatedAt.AddSeconds(ResendCooldownSeconds) > now)
            throw new AppException("Please wait a moment before requesting another code.", StatusCodes.Status429TooManyRequests);

        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
        _db.OtpVerifications.Add(new OtpVerification
        {
            Identifier = identifier,
            Channel = channel,
            Purpose = purpose,
            CodeHash = _hasher.Hash(code),
            ExpiresAt = now.AddMinutes(ExpiryMinutes),
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);

        var message = $"Your verification code is {code}. It expires in {ExpiryMinutes} minutes.";
        if (channel == "SMS")
        {
            // SMS is admin-toggleable (Settings.ChannelSMSEnabled) — same kill switch
            // NotificationRouter uses for WhatsApp/SMS in the order-notification chain. While it's
            // off (e.g. SMS is blocked on DLT registration), fall back to emailing the same code to
            // whatever address this phone number's account already has on file, rather than sending
            // a code nobody will ever receive.
            if (await _channelSettings.IsEnabledAsync("SMS", ct))
            {
                await _sms.SendAsync(identifier, message, ct);
            }
            else if (EmailTemplates.TryGetValue(purpose, out var fallbackTemplateCode))
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == identifier, ct);
                if (user is null || string.IsNullOrWhiteSpace(user.Email))
                    throw new AppException("SMS sign-in is temporarily unavailable, and there's no email on file for this number to send a code to instead. Please try a different sign-in method.");
                await SendEmailOtpAsync(fallbackTemplateCode, user.Email!, user.FullName ?? "there", code, ct);
            }
            else
            {
                throw new AppException("SMS sign-in is temporarily unavailable. Please try a different sign-in method.");
            }
        }
        else if (channel == "Email" && EmailTemplates.TryGetValue(purpose, out var templateCode))
        {
            var name = await _db.Users.Where(u => u.NormalizedEmail == identifier.ToUpperInvariant())
                .Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "there";
            await SendEmailOtpAsync(templateCode, identifier, name!, code, ct);
        }
        else
        {
            _logger.LogWarning("[DEV OTP/{Channel}] {Identifier}: {Code}", channel, identifier, code);
        }
    }

    private async Task SendEmailOtpAsync(string templateCode, string email, string name, string code, CancellationToken ct)
    {
        var storeName = await _db.Settings.Where(s => s.SettingKey == "SiteName")
            .Select(s => s.SettingValue).FirstOrDefaultAsync(ct) ?? "CalendarShop";
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OtpCode"] = code, ["CustomerName"] = name, ["StoreName"] = storeName!,
        };
        await _notify.SendEmailAsync(templateCode, email, tokens, ct);
    }

    public async Task<bool> VerifyAsync(string identifier, string purpose, string code, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var otp = await _db.OtpVerifications
            .Where(o => o.Identifier == identifier && o.Purpose == purpose && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (otp is null) throw new AppException("No active code found. Please request a new one.");
        if (otp.ExpiresAt < now) throw new AppException("The code has expired. Please request a new one.");
        if (otp.AttemptCount >= otp.MaxAttempts)
            throw new AppException("Too many attempts. Please request a new code.", StatusCodes.Status429TooManyRequests);

        otp.AttemptCount++;
        if (!_hasher.Verify(code, otp.CodeHash))
        {
            await _db.SaveChangesAsync(ct);
            return false;
        }

        otp.ConsumedAt = now;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
