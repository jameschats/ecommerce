using ecomm.api.Common.Exceptions;
using ecomm.api.Features.Account;
using Xunit;

namespace ecomm.tests;

public class NotificationPreferenceTests
{
    [Fact]
    public async Task Setting_a_preference_creates_it_and_stamps_OptedInAt()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new AccountService(db);

        var dto = await svc.SetNotificationPreferenceAsync(7, new SetNotificationPreferenceRequest("Email", "marketing", true));

        Assert.True(dto.IsOptedIn);
        Assert.NotNull(dto.OptedInAt);
        Assert.Single(await svc.ListNotificationPreferencesAsync(7));
    }

    [Fact]
    public async Task Opting_out_clears_OptedInAt()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new AccountService(db);
        await svc.SetNotificationPreferenceAsync(7, new SetNotificationPreferenceRequest("Email", "marketing", true));

        var dto = await svc.SetNotificationPreferenceAsync(7, new SetNotificationPreferenceRequest("Email", "marketing", false));

        Assert.False(dto.IsOptedIn);
        Assert.Null(dto.OptedInAt);
    }

    [Fact]
    public async Task Re_opting_in_stamps_a_fresh_consent_timestamp()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new AccountService(db);
        var first = await svc.SetNotificationPreferenceAsync(7, new SetNotificationPreferenceRequest("Email", "marketing", true));
        await svc.SetNotificationPreferenceAsync(7, new SetNotificationPreferenceRequest("Email", "marketing", false));

        var second = await svc.SetNotificationPreferenceAsync(7, new SetNotificationPreferenceRequest("Email", "marketing", true));

        Assert.NotNull(second.OptedInAt);
        Assert.True(second.OptedInAt >= first.OptedInAt);
    }

    [Fact]
    public async Task Setting_the_same_channel_and_category_twice_updates_the_same_row_not_a_duplicate()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new AccountService(db);
        await svc.SetNotificationPreferenceAsync(7, new SetNotificationPreferenceRequest("WhatsApp", "marketing", true));
        await svc.SetNotificationPreferenceAsync(7, new SetNotificationPreferenceRequest("WhatsApp", "marketing", false));

        Assert.Single(await svc.ListNotificationPreferencesAsync(7));
    }

    [Fact]
    public async Task Invalid_category_is_rejected()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new AccountService(db);

        await Assert.ThrowsAsync<AppException>(() =>
            svc.SetNotificationPreferenceAsync(7, new SetNotificationPreferenceRequest("Email", "spam", true)));
    }

    [Fact]
    public async Task Preferences_are_scoped_per_user()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new AccountService(db);
        await svc.SetNotificationPreferenceAsync(7, new SetNotificationPreferenceRequest("Email", "marketing", true));
        await svc.SetNotificationPreferenceAsync(9, new SetNotificationPreferenceRequest("Email", "marketing", true));

        Assert.Single(await svc.ListNotificationPreferencesAsync(7));
        Assert.Single(await svc.ListNotificationPreferencesAsync(9));
    }
}
