using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.MarketingStudio;
using Xunit;

namespace ecomm.tests;

public class MarketingBrandServiceTests
{
    /// <summary>Theme-defaults port stub — returns a fixed palette (or none).</summary>
    private sealed class FakeThemeDefaults((string, string, string)? palette) : IBrandThemeDefaults
    {
        public Task<(string Primary, string Secondary, string Accent)?> TryGetAsync(CancellationToken ct = default)
            => Task.FromResult(palette);
    }

    private static MarketingBrandService New(EcommerceDbContext db, (string, string, string)? theme = null) =>
        new(db, new FakeThemeDefaults(theme));

    [Fact]
    public async Task Get_with_no_profile_seeds_colours_from_the_active_theme()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, theme: ("#aa0000", "#00bb00", "#0000cc"));

        var brand = await svc.GetAsync();

        Assert.Equal("#aa0000", brand.PrimaryColor);
        Assert.Equal("#00bb00", brand.SecondaryColor);
        Assert.Equal("#0000cc", brand.AccentColor);
        Assert.True(brand.IncludeLogoByDefault);
        Assert.Null(brand.CompanyName);
    }

    [Fact]
    public async Task Get_with_no_profile_and_no_theme_falls_back_to_neutrals()
    {
        using var db = TestDb.New(tenantId: 1);
        var brand = await New(db).GetAsync();
        Assert.Equal("#111827", brand.PrimaryColor);
    }

    [Fact]
    public async Task Save_persists_and_reload_returns_the_saved_values()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db);

        var input = new MarketingBrandDto(
            "Meenakshi Silks", "Handwoven since 1985", "https://cdn/logo.png",
            "#123456", "#abcdef", "#0d9488", "Poppins", false, true,
            "@silks", null, null, null, null, "+919000000000", "https://silks.example");

        await svc.SaveAsync(input);
        var reloaded = await svc.GetAsync();

        Assert.Equal("Meenakshi Silks", reloaded.CompanyName);
        Assert.Equal("#123456", reloaded.PrimaryColor);
        Assert.False(reloaded.IncludeLogoByDefault);
        Assert.Equal("@silks", reloaded.InstagramHandle);
        Assert.Equal(1, db.MarketingBrandProfiles.Count());   // upsert, not duplicate
    }

    [Fact]
    public async Task Save_twice_updates_the_same_row()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db);
        var baseDto = new MarketingBrandDto(null, null, null, "#111827", "#6b7280", "#2563eb", null, true, true,
            null, null, null, null, null, null, null);

        await svc.SaveAsync(baseDto with { CompanyName = "First" });
        await svc.SaveAsync(baseDto with { CompanyName = "Second" });

        Assert.Equal(1, db.MarketingBrandProfiles.Count());
        Assert.Equal("Second", (await svc.GetAsync()).CompanyName);
    }

    [Fact]
    public async Task Save_rejects_an_invalid_hex_colour_and_keeps_the_fallback()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db);
        var dto = new MarketingBrandDto(null, null, null, "not-a-colour", "#abc", "#2563eb", null, true, true,
            null, null, null, null, null, null, null);

        var saved = await svc.SaveAsync(dto);

        Assert.Equal("#111827", saved.PrimaryColor);   // invalid → fallback
        Assert.Equal("#abc", saved.SecondaryColor);    // #rgb shorthand is valid
    }
}
