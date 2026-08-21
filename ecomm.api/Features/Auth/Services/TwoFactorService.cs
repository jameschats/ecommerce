using System.Security.Cryptography;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace ecomm.api.Features.Auth.Services;

public sealed record TwoFactorEnrollmentDto(string Secret, string ProvisioningUri);

public interface ITwoFactorService
{
    /// <summary>Generates a new (unconfirmed) TOTP secret for the user — stored but NOT enabled until
    /// <see cref="ConfirmEnrollmentAsync"/> proves the user actually captured it correctly. Safe to
    /// call again before confirming (overwrites the pending secret); has no effect on an already-
    /// confirmed enrollment (throws — must Disable first to re-enroll).</summary>
    Task<TwoFactorEnrollmentDto> BeginEnrollmentAsync(long userId, CancellationToken ct = default);

    /// <summary>Confirms enrollment with a real generated code, flips TwoFactorEnabled on, and issues
    /// 10 single-use backup codes (returned once, plaintext — never retrievable again).</summary>
    Task<IReadOnlyList<string>> ConfirmEnrollmentAsync(long userId, string code, CancellationToken ct = default);

    /// <summary>Turns 2FA off and clears the secret + any unused backup codes. Requires the current
    /// TOTP/backup code, not just being logged in — disabling 2FA is itself a security-sensitive
    /// action, same reasoning as requiring a password to change a password.</summary>
    Task DisableAsync(long userId, string code, CancellationToken ct = default);

    /// <summary>TOTP code OR a single-use backup code. Backup codes are consumed on successful use.</summary>
    Task<bool> VerifyAsync(long userId, string code, CancellationToken ct = default);

    /// <summary>Invalidates all existing backup codes and issues 10 new ones. Requires the current
    /// TOTP code (same reasoning as Disable).</summary>
    Task<IReadOnlyList<string>> RegenerateBackupCodesAsync(long userId, string code, CancellationToken ct = default);

    Task<bool> IsEnabledAsync(long userId, CancellationToken ct = default);
}

/// <summary>
/// TOTP-based 2FA for Merchant Admin / Super Admin / Staff (v4 Phase 1). SMS is the documented
/// fallback method (design doc) — reuses the existing <see cref="OtpService"/> as-is rather than a
/// second OTP system; this service owns only the TOTP + backup-code path. The secret is
/// DataProtection-encrypted at rest, same pattern as TenantPaymentAccounts' Razorpay secret
/// (<see cref="ecomm.api.Features.Payments.PaymentSettingsService"/>) — never stored or logged plain.
/// </summary>
public sealed class TwoFactorService(EcommerceDbContext db, IDataProtectionProvider dp) : ITwoFactorService
{
    public const string ProtectorPurpose = "auth.2fa.secret.v1";
    private const int BackupCodeCount = 10;
    private const string Issuer = "WavCommerce";

    private IDataProtector Protector => dp.CreateProtector(ProtectorPurpose);

    public async Task<TwoFactorEnrollmentDto> BeginEnrollmentAsync(long userId, CancellationToken ct = default)
    {
        var user = await Find(userId, ct);
        if (user.TwoFactorEnabled) throw new AppException("2FA is already enabled. Disable it first to re-enroll.", 400);

        var key = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(key);
        user.TwoFactorSecret = Protector.Protect(secret);
        await db.SaveChangesAsync(ct);

        var label = Uri.EscapeDataString($"{Issuer}:{user.Email ?? user.PhoneNumber ?? "account"}");
        var uri = $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(Issuer)}&algorithm=SHA1&digits=6&period=30";
        return new TwoFactorEnrollmentDto(secret, uri);
    }

    public async Task<IReadOnlyList<string>> ConfirmEnrollmentAsync(long userId, string code, CancellationToken ct = default)
    {
        var user = await Find(userId, ct);
        if (user.TwoFactorEnabled) throw new AppException("2FA is already enabled.", 400);
        if (string.IsNullOrWhiteSpace(user.TwoFactorSecret)) throw new AppException("Start enrollment first.", 400);
        if (!VerifyTotp(user.TwoFactorSecret, code)) throw new AppException("Incorrect code. Check your authenticator app and try again.", 400);

        user.TwoFactorEnabled = true;
        user.TwoFactorEnabledAt = DateTime.UtcNow;
        var codes = await IssueBackupCodesAsync(user, ct);
        await db.SaveChangesAsync(ct);
        return codes;
    }

    public async Task DisableAsync(long userId, string code, CancellationToken ct = default)
    {
        var user = await Find(userId, ct);
        if (!user.TwoFactorEnabled) return;
        if (!await VerifyAsync(userId, code, ct)) throw new AppException("Incorrect code.", 400);

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorEnabledAt = null;
        db.UserTwoFactorBackupCodes.RemoveRange(user.TwoFactorBackupCodes);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> VerifyAsync(long userId, string code, CancellationToken ct = default)
    {
        var user = await Find(userId, ct);
        if (!user.TwoFactorEnabled || string.IsNullOrWhiteSpace(user.TwoFactorSecret)) return false;
        code = (code ?? "").Trim();

        if (VerifyTotp(user.TwoFactorSecret, code)) return true;

        // Not a TOTP match — try backup codes (hashed, single-use).
        var hash = HashBackupCode(code);
        var backup = await db.UserTwoFactorBackupCodes
            .FirstOrDefaultAsync(b => b.UserId == userId && b.CodeHash == hash && b.UsedAtUtc == null, ct);
        if (backup is null) return false;

        backup.UsedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<string>> RegenerateBackupCodesAsync(long userId, string code, CancellationToken ct = default)
    {
        var user = await Find(userId, ct);
        if (!user.TwoFactorEnabled) throw new AppException("2FA is not enabled.", 400);
        if (!VerifyTotp(user.TwoFactorSecret!, code)) throw new AppException("Incorrect code.", 400);

        db.UserTwoFactorBackupCodes.RemoveRange(user.TwoFactorBackupCodes);
        var codes = await IssueBackupCodesAsync(user, ct);
        await db.SaveChangesAsync(ct);
        return codes;
    }

    public async Task<bool> IsEnabledAsync(long userId, CancellationToken ct = default) =>
        await db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.TwoFactorEnabled).FirstOrDefaultAsync(ct);

    // ---- helpers ----

    private async Task<User> Find(long userId, CancellationToken ct) =>
        await db.Users.Include(u => u.TwoFactorBackupCodes).FirstOrDefaultAsync(u => u.UserId == userId, ct)
        ?? throw new AppException("User not found.", 404);

    private bool VerifyTotp(string protectedSecret, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        string secret;
        try { secret = Protector.Unprotect(protectedSecret); }
        catch { return false; }   // corrupt/rotated key material — fail closed, not open
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(1, 1));
    }

    private async Task<IReadOnlyList<string>> IssueBackupCodesAsync(User user, CancellationToken ct)
    {
        var plaintextCodes = new List<string>(BackupCodeCount);
        var now = DateTime.UtcNow;
        for (var i = 0; i < BackupCodeCount; i++)
        {
            var code = GenerateBackupCode();
            plaintextCodes.Add(code);
            db.UserTwoFactorBackupCodes.Add(new UserTwoFactorBackupCode
            {
                UserId = user.UserId, CodeHash = HashBackupCode(code), CreatedAt = now,
            });
        }
        await Task.CompletedTask;   // keep signature async-consistent with the rest of the service
        return plaintextCodes;
    }

    /// <summary>8-character, unambiguous-alphabet backup code (no 0/O/1/I) — e.g. "7K4M9XPQ".</summary>
    private static string GenerateBackupCode()
    {
        const string alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        Span<char> buf = stackalloc char[8];
        for (var i = 0; i < buf.Length; i++) buf[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(buf);
    }

    private static string HashBackupCode(string code) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));
}
