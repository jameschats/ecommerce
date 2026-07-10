using ecomm.api.Features.Storefront;
using Xunit;

namespace ecomm.tests;

public class StorefrontPreferencesTests
{
    [Fact]
    public async Task Seo_round_trips_and_is_public()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new StorefrontPreferencesService(db);

        await svc.UpdateAsync(new UpdateStorefrontPreferencesRequest(
            "My Store", "The best store", "https://cdn/x.png", false, null, null));

        var seo = await svc.GetSeoAsync();
        Assert.Equal("My Store", seo.Title);
        Assert.Equal("The best store", seo.Description);
        Assert.Equal("https://cdn/x.png", seo.Image);
    }

    [Fact]
    public async Task Gate_off_allows_any_password()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new StorefrontPreferencesService(db);

        var gate = await svc.GetGateAsync();
        Assert.False(gate.PasswordProtected);
        Assert.True(await svc.CheckPasswordAsync(null));
        Assert.True(await svc.CheckPasswordAsync("anything"));
    }

    [Fact]
    public async Task Gate_on_enforces_password_and_keeps_it_on_update()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new StorefrontPreferencesService(db);

        await svc.UpdateAsync(new UpdateStorefrontPreferencesRequest(
            null, null, null, true, "s3cret", "Coming soon"));

        var gate = await svc.GetGateAsync();
        Assert.True(gate.PasswordProtected);
        Assert.Equal("Coming soon", gate.Message);
        Assert.True(await svc.CheckPasswordAsync("s3cret"));
        Assert.False(await svc.CheckPasswordAsync("wrong"));

        // Updating other fields without re-sending the password keeps the existing one.
        await svc.UpdateAsync(new UpdateStorefrontPreferencesRequest(
            "Title", null, null, true, null, "Still soon"));
        Assert.True(await svc.CheckPasswordAsync("s3cret"));
    }

    [Fact]
    public async Task Gate_enabled_without_password_is_not_protected()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new StorefrontPreferencesService(db);

        // Enabled flag on, but no password ever set → gate reports off (don't lock out with no key).
        await svc.UpdateAsync(new UpdateStorefrontPreferencesRequest(
            null, null, null, true, null, null));

        var gate = await svc.GetGateAsync();
        Assert.False(gate.PasswordProtected);   // no key set → don't advertise a gate the visitor can't pass
    }
}
