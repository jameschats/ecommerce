using System.Security.Cryptography;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
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

    private readonly EcommerceDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ISmsSender _sms;
    private readonly ILogger<OtpService> _logger;

    public OtpService(EcommerceDbContext db, IPasswordHasher hasher, ISmsSender sms, ILogger<OtpService> logger)
    {
        _db = db;
        _hasher = hasher;
        _sms = sms;
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
            await _sms.SendAsync(identifier, message, ct);
        else
            _logger.LogWarning("[DEV OTP/{Channel}] {Identifier}: {Code}", channel, identifier, code);
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
