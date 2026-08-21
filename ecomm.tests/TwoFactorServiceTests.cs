using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using Xunit;

namespace ecomm.tests;

public class TwoFactorServiceTests
{
    private static (ecomm.api.Data.Context.EcommerceDbContext db, TwoFactorService svc, long userId) NewEnrolled()
    {
        var db = TestDb.New(tenantId: 1);
        var user = new User { TenantId = 1, Email = "admin@store.test", NormalizedEmail = "ADMIN@STORE.TEST", CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        db.SaveChanges();
        var svc = new TwoFactorService(db, DataProtectionProvider.Create("ecomm.tests"));
        return (db, svc, user.UserId);
    }

    /// <summary>Reads the (encrypted) secret back off the user row and computes a real, currently-valid
    /// TOTP code for it — same as scanning the QR code into an authenticator app and reading the digits.</summary>
    private static string CurrentCode(ecomm.api.Data.Context.EcommerceDbContext db, long userId)
    {
        var protector = DataProtectionProvider.Create("ecomm.tests")
            .CreateProtector(TwoFactorService.ProtectorPurpose);
        var protectedSecret = db.Users.AsNoTracking().Single(u => u.UserId == userId).TwoFactorSecret!;
        var secret = protector.Unprotect(protectedSecret);
        return new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
    }

    [Fact]
    public async Task Not_enabled_until_confirmed_with_a_real_code()
    {
        var (db, svc, userId) = NewEnrolled();
        Assert.False(await svc.IsEnabledAsync(userId));

        await svc.BeginEnrollmentAsync(userId);
        Assert.False(await svc.IsEnabledAsync(userId));   // began, not confirmed — still off

        await svc.ConfirmEnrollmentAsync(userId, CurrentCode(db, userId));
        Assert.True(await svc.IsEnabledAsync(userId));
    }

    [Fact]
    public async Task Confirm_rejects_a_wrong_code_and_stays_disabled()
    {
        var (_, svc, userId) = NewEnrolled();
        await svc.BeginEnrollmentAsync(userId);

        await Assert.ThrowsAsync<AppException>(() => svc.ConfirmEnrollmentAsync(userId, "000000"));
        Assert.False(await svc.IsEnabledAsync(userId));
    }

    [Fact]
    public async Task Confirm_issues_ten_backup_codes_and_they_are_single_use()
    {
        var (db, svc, userId) = NewEnrolled();
        await svc.BeginEnrollmentAsync(userId);
        var codes = await svc.ConfirmEnrollmentAsync(userId, CurrentCode(db, userId));

        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Distinct().Count());

        var backupCode = codes[0];
        Assert.True(await svc.VerifyAsync(userId, backupCode));    // first use: valid
        Assert.False(await svc.VerifyAsync(userId, backupCode));   // second use: already consumed
    }

    [Fact]
    public async Task Verify_accepts_a_real_totp_code_and_rejects_a_wrong_one()
    {
        var (db, svc, userId) = NewEnrolled();
        await svc.BeginEnrollmentAsync(userId);
        await svc.ConfirmEnrollmentAsync(userId, CurrentCode(db, userId));

        Assert.True(await svc.VerifyAsync(userId, CurrentCode(db, userId)));
        Assert.False(await svc.VerifyAsync(userId, "111111"));
    }

    [Fact]
    public async Task Disable_requires_a_correct_code_and_clears_everything()
    {
        var (db, svc, userId) = NewEnrolled();
        await svc.BeginEnrollmentAsync(userId);
        await svc.ConfirmEnrollmentAsync(userId, CurrentCode(db, userId));

        await Assert.ThrowsAsync<AppException>(() => svc.DisableAsync(userId, "000000"));
        Assert.True(await svc.IsEnabledAsync(userId));   // wrong code — still enabled

        await svc.DisableAsync(userId, CurrentCode(db, userId));
        Assert.False(await svc.IsEnabledAsync(userId));
        var user = await db.Users.AsNoTracking().Include(u => u.TwoFactorBackupCodes).SingleAsync(u => u.UserId == userId);
        Assert.Null(user.TwoFactorSecret);
        Assert.Empty(user.TwoFactorBackupCodes);
    }

    [Fact]
    public async Task Regenerate_invalidates_the_old_backup_codes()
    {
        var (db, svc, userId) = NewEnrolled();
        await svc.BeginEnrollmentAsync(userId);
        var original = await svc.ConfirmEnrollmentAsync(userId, CurrentCode(db, userId));

        var fresh = await svc.RegenerateBackupCodesAsync(userId, CurrentCode(db, userId));

        Assert.Equal(10, fresh.Count);
        Assert.False(await svc.VerifyAsync(userId, original[0]));   // old code no longer works
        Assert.True(await svc.VerifyAsync(userId, fresh[0]));       // new one does
    }

    [Fact]
    public async Task Cannot_begin_enrollment_twice_without_disabling_first()
    {
        var (db, svc, userId) = NewEnrolled();
        await svc.BeginEnrollmentAsync(userId);
        await svc.ConfirmEnrollmentAsync(userId, CurrentCode(db, userId));

        await Assert.ThrowsAsync<AppException>(() => svc.BeginEnrollmentAsync(userId));
    }
}
