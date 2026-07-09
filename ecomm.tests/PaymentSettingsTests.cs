using ecomm.api.Common.Exceptions;
using ecomm.api.Features.Payments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class PaymentSettingsTests
{
    private static (PaymentSettingsService svc, ecomm.api.Data.Context.EcommerceDbContext db, IDataProtectionProvider dp) New()
    {
        var db = TestDb.New(tenantId: 1);
        var dp = new EphemeralDataProtectionProvider();
        return (new PaymentSettingsService(db, dp), db, dp);
    }

    [Fact]
    public async Task Enabling_razorpay_without_keys_is_rejected()
    {
        var (svc, db, _) = New();
        using var _d = db;
        await Assert.ThrowsAsync<AppException>(
            () => svc.UpdateAsync(new UpdatePaymentSettingsRequest("Razorpay", null, null, true, false)));
    }

    [Fact]
    public async Task Secret_is_encrypted_at_rest_and_never_returned()
    {
        var (svc, db, dp) = New();
        using var _d = db;

        var dto = await svc.UpdateAsync(new UpdatePaymentSettingsRequest("Razorpay", "rzp_test_1", "supersecret", true, true));

        Assert.True(dto.HasSecret);
        Assert.True(dto.IsEnabled);
        Assert.True(dto.CodEnabled);
        Assert.Equal("rzp_test_1", dto.RazorpayKeyId);

        var acct = await db.TenantPaymentAccounts.SingleAsync();
        Assert.NotEqual("supersecret", acct.RazorpayKeySecret);   // ciphertext, not plaintext
        Assert.Equal("supersecret", dp.CreateProtector(PaymentSettingsService.ProtectorPurpose).Unprotect(acct.RazorpayKeySecret!));
    }

    [Fact]
    public async Task Blank_secret_on_update_keeps_the_existing_one()
    {
        var (svc, db, _) = New();
        using var _d = db;

        await svc.UpdateAsync(new UpdatePaymentSettingsRequest("Razorpay", "rzp_test_1", "supersecret", true, true));
        var dto = await svc.UpdateAsync(new UpdatePaymentSettingsRequest("Razorpay", "rzp_test_2", null, true, false));

        Assert.True(dto.HasSecret);            // secret preserved
        Assert.Equal("rzp_test_2", dto.RazorpayKeyId);
        Assert.False(dto.CodEnabled);
    }
}
