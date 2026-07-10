using ecomm.api.Common.Exceptions;
using ecomm.api.Features.Settings;
using Xunit;

namespace ecomm.tests;

public class CheckoutSettingsTests
{
    private static UpdateCheckoutSettingsRequest Req(
        string contact = "email", bool phone = false, bool tip = false, string? presets = null,
        int limit = 0, bool cancel = true, bool returns = false)
        => new(contact, phone, tip, presets, limit, cancel, returns);

    [Fact]
    public async Task Defaults_are_sensible_for_a_fresh_store()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new CheckoutSettingsService(db);

        var s = await svc.GetAsync();
        Assert.Equal("email", s.ContactMethod);
        Assert.False(s.RequirePhone);
        Assert.Equal(0, s.ItemLimit);
        Assert.True(s.SelfServeCancel);     // cancellation on by default (existing behaviour)
        Assert.False(s.SelfServeReturns);
    }

    [Fact]
    public async Task Round_trips_and_normalizes_tip_presets()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new CheckoutSettingsService(db);

        await svc.UpdateAsync(Req(contact: "phone", phone: true, tip: true, presets: "15, 5 ,10,10,x", limit: 3, cancel: false));

        var s = await svc.GetAsync();
        Assert.Equal("phone", s.ContactMethod);
        Assert.True(s.RequirePhone);
        Assert.Equal(3, s.ItemLimit);
        Assert.False(s.SelfServeCancel);
        Assert.Equal("5,10,15", s.TipPresets);   // deduped, ordered, non-numeric dropped

        var pub = await svc.GetPublicAsync();
        Assert.Equal(new[] { 5, 10, 15 }, pub.TipPresets);
    }

    [Fact]
    public async Task Rejects_bad_contact_method_and_negative_limit()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new CheckoutSettingsService(db);

        await Assert.ThrowsAsync<AppException>(() => svc.UpdateAsync(Req(contact: "carrier-pigeon")));
        await Assert.ThrowsAsync<AppException>(() => svc.UpdateAsync(Req(limit: -1)));
    }
}
