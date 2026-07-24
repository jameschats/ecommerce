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

    // Email OTP purposes → the notification template used to deliver the code.
    private static readonly Dictionary<string, string> EmailTemplates = new()
    {
        ["ResetPassword"] = "PasswordReset",
        ["VerifyEmail"] = "EmailVerification",
    };

    private readonly EcommerceDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ISmsSender _sms;
    private readonly INotificationService _notify;
    private readonly ILogger<OtpService> _logger;

    public OtpService(EcommerceDbContext db, IPasswordHasher hasher, ISmsSender sms, INotificationService notify, ILogger<OtpService> logger)
    {
        _db = db;
        _hasher = hasher;
        _sms = sms;
        _notify = notify;
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

        // Mock mode differs from Live in exactly two ways: the code is a fixed known value
        // instead of a random one, and it is never handed to a paid provider. Everything
        // else below — hashing, expiry, cooldown, attempt limits, and the whole of
        // VerifyAsync — is identical, so switching to Live is not the first time that
        // logic runs. See design.md §9.1.
        var mock = await IsMockAsync(channel, ct);
        var code = mock
            ? await MockCodeAsync(ct)
            : RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
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

        if (mock)
        {
            // Deliberately logged at Warning: a mocked OTP in a running environment is
            // something an operator should notice in the log, not have to go looking for.
            _logger.LogWarning("[MOCK OTP/{Channel}] {Identifier}: {Code}", channel, identifier, code);
            return;
        }

        if (channel == "SMS")
        {
            await _sms.SendAsync(identifier, message, ct);
        }
        else if (channel == "Email" && EmailTemplates.TryGetValue(purpose, out var templateCode))
        {
            var name = await _db.Users.Where(u => u.NormalizedEmail == identifier.ToUpperInvariant())
                .Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "there";
            var storeName = await _db.Settings.Where(s => s.SettingKey == "SiteName")
                .Select(s => s.SettingValue).FirstOrDefaultAsync(ct) ?? "CalendarShop";
            var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["OtpCode"] = code, ["CustomerName"] = name!, ["StoreName"] = storeName!,
            };
            await _notify.SendEmailAsync(templateCode, identifier, tokens, ct);
        }
        else
        {
            _logger.LogWarning("[DEV OTP/{Channel}] {Identifier}: {Code}", channel, identifier, code);
        }
    }

    /// <summary>
    /// Whether this channel is mocked. Read from the database rather than configuration so
    /// an admin can flip it without a redeploy (design.md §9.2). Unknown or missing values
    /// fall back to <c>Mock</c>: a channel we cannot resolve should not be silently trying
    /// to spend money on a paid provider.
    /// </summary>
    private async Task<bool> IsMockAsync(string channel, CancellationToken ct)
    {
        var key = channel == "SMS" ? "Channels.SmsMode" : "Channels.EmailMode";
        var mode = await _db.Settings
            .Where(s => s.SettingKey == key)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(ct);

        return !string.Equals(mode, "Live", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The fixed test code. A setting rather than a literal, so it can be changed without
    /// a deploy — and so it is obvious in the database that one exists.
    /// </summary>
    private async Task<string> MockCodeAsync(CancellationToken ct)
    {
        var code = await _db.Settings
            .Where(s => s.SettingKey == "Channels.OtpMockCode")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(code) ? "000000" : code;
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
