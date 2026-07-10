using ecomm.api.Data.Entities;
using ecomm.api.Features.Cms.SectionTypes;
using ecomm.api.Features.Storefront;
using Xunit;

namespace ecomm.tests;

public class SectionRegistryTests
{
    [Fact]
    public void Group_and_dynamic_types_declare_kind_and_scope()
    {
        Assert.Equal("group", SectionTypeRegistry.Get("Header")!.Kind);
        Assert.Equal("group", SectionTypeRegistry.Get("Footer")!.Kind);
        Assert.Equal("dynamic", SectionTypeRegistry.Get("ProductGallery")!.Kind);
        Assert.Equal("static", SectionTypeRegistry.Get("Hero")!.Kind);   // existing types default to static
    }

    [Fact]
    public void Scope_gates_which_template_a_section_is_valid_on()
    {
        Assert.True(SectionTypeRegistry.IsValidOnTemplate("ProductGallery", "product"));
        Assert.False(SectionTypeRegistry.IsValidOnTemplate("ProductGallery", "index"));
        Assert.True(SectionTypeRegistry.IsValidOnTemplate("Hero", "index"));          // scope null → any
        Assert.True(SectionTypeRegistry.IsValidOnTemplate("CartItems", "cart"));
        Assert.False(SectionTypeRegistry.IsValidOnTemplate("CartItems", "product"));
    }

    [Fact]
    public void ForTemplate_lists_valid_sections_only()
    {
        var product = SectionTypeRegistry.ForTemplate("product").Select(s => s.Key).ToHashSet();
        Assert.Contains("ProductInfo", product);
        Assert.Contains("Hero", product);            // any-scope statics are allowed everywhere
        Assert.DoesNotContain("CartItems", product);
    }

    [Fact]
    public void Template_keys_are_recognised()
    {
        Assert.True(SectionTypeRegistry.IsValidTemplateKey("index"));
        Assert.True(SectionTypeRegistry.IsValidTemplateKey("product"));
        Assert.False(SectionTypeRegistry.IsValidTemplateKey("nonsense"));
    }
}

public class StorefrontThemeServiceTests
{
    private static async Task<ecomm.api.Data.Context.EcommerceDbContext> SeedAsync()
    {
        var db = TestDb.New(tenantId: 1);
        var theme = new Theme { Name = "Default", IsActive = true, Status = "Published", CreatedAt = DateTime.UtcNow };
        db.Themes.Add(theme);
        await db.SaveChangesAsync();
        db.ThemeSettings.Add(new ThemeSetting { ThemeId = theme.ThemeId, SettingKey = "primaryColor", SettingValue = "#2563eb", CreatedAt = DateTime.UtcNow });

        var home = new Page { Title = "Home", Slug = "home", Type = "Home", IsPublished = true, CreatedAt = DateTime.UtcNow };
        db.Pages.Add(home);
        await db.SaveChangesAsync();
        db.PageSections.Add(new PageSection { PageId = home.PageId, SectionType = "Hero", Title = "Hero", DisplayOrder = 1, IsVisible = true, CreatedAt = DateTime.UtcNow });
        db.PageSections.Add(new PageSection { PageId = home.PageId, SectionType = "RichText", Title = "Copy", DisplayOrder = 2, IsVisible = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Index_template_falls_back_to_home_sections_before_backfill()
    {
        using var db = await SeedAsync();
        var svc = new StorefrontThemeService(db);

        var t = await svc.GetTemplateAsync("index");

        Assert.Equal("index", t.TemplateKey);
        Assert.Equal(2, t.Sections.Count);
        Assert.Equal("Hero", t.Sections[0].SectionType);
        Assert.Equal("static", t.Sections[0].Kind);   // Kind resolved from the registry
    }

    [Fact]
    public async Task Backfill_copies_home_into_index_and_is_idempotent()
    {
        using var db = await SeedAsync();
        var svc = new StorefrontThemeService(db);

        Assert.Equal(2, await svc.BackfillIndexFromHomeAsync());   // seeded
        Assert.Equal(0, await svc.BackfillIndexFromHomeAsync());   // already populated → no-op

        // Now the index template serves persisted ThemeSections (still 2), not the fallback.
        var t = await svc.GetTemplateAsync("index");
        Assert.Equal(2, t.Sections.Count);
    }

    [Fact]
    public async Task Published_bundle_returns_settings_and_empty_zones()
    {
        using var db = await SeedAsync();
        var svc = new StorefrontThemeService(db);

        var bundle = await svc.GetPublishedBundleAsync();

        Assert.Equal("Published", bundle.Status);
        Assert.Equal("#2563eb", bundle.Settings["primaryColor"]);
        Assert.Empty(bundle.Header);
        Assert.Empty(bundle.Footer);
        Assert.Empty(bundle.Announcement);
    }

    [Fact]
    public async Task Unknown_template_key_returns_empty()
    {
        using var db = await SeedAsync();
        var svc = new StorefrontThemeService(db);
        var t = await svc.GetTemplateAsync("nonsense");
        Assert.Empty(t.Sections);
    }
}
