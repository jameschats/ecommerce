using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Storefront;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class ThemeLibraryTests
{
    private static async Task<(ecomm.api.Data.Context.EcommerceDbContext db, ThemeLibraryService lib)> SeedPublishedAsync()
    {
        var db = TestDb.New(tenantId: 1);
        db.Themes.Add(new Theme { Name = "Live", IsActive = true, Status = "Published", PreviewToken = "live-token", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return (db, new ThemeLibraryService(db));
    }

    [Fact]
    public async Task Create_makes_a_draft_with_a_preview_token()
    {
        var (db, lib) = await SeedPublishedAsync();
        using var _ = db;

        var draft = await lib.CreateAsync("Summer");

        Assert.Equal("Draft", draft.Status);
        Assert.False(draft.IsPublished);
        Assert.False(string.IsNullOrEmpty(draft.PreviewToken));
    }

    [Fact]
    public async Task Publish_swaps_exactly_one_published()
    {
        var (db, lib) = await SeedPublishedAsync();
        using var _ = db;
        var draft = await lib.CreateAsync("Summer");

        await lib.PublishAsync(draft.ThemeId);

        var all = await db.Themes.IgnoreQueryFilters().ToListAsync();
        Assert.Single(all, t => t.Status == "Published");
        var live = all.Single(t => t.Status == "Published");
        Assert.Equal(draft.ThemeId, live.ThemeId);
        Assert.True(live.IsActive);
        Assert.All(all.Where(t => t.ThemeId != draft.ThemeId), t => Assert.False(t.IsActive));
    }

    [Fact]
    public async Task Duplicate_deep_copies_settings_templates_and_sections()
    {
        var (db, lib) = await SeedPublishedAsync();
        using var _ = db;
        var src = (await lib.ListAsync()).Single();
        db.ThemeSettings.Add(new ThemeSetting { ThemeId = src.ThemeId, SettingKey = "PrimaryColor", SettingValue = "#123456", CreatedAt = DateTime.UtcNow });
        var tpl = new ThemeTemplate { ThemeId = src.ThemeId, TemplateKey = "index", Name = "Default", CreatedAt = DateTime.UtcNow };
        db.ThemeTemplates.Add(tpl);
        await db.SaveChangesAsync();
        db.ThemeSections.Add(new ThemeSection { ThemeTemplateId = tpl.ThemeTemplateId, SectionType = "Hero", DisplayOrder = 1, IsVisible = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var copy = await lib.DuplicateAsync(src.ThemeId, "Copy");

        Assert.Equal("Draft", copy.Status);
        Assert.NotEqual(src.ThemeId, copy.ThemeId);
        Assert.Equal("#123456", await db.ThemeSettings.Where(s => s.ThemeId == copy.ThemeId && s.SettingKey == "PrimaryColor").Select(s => s.SettingValue).FirstAsync());
        var copiedTpl = await db.ThemeTemplates.SingleAsync(t => t.ThemeId == copy.ThemeId && t.TemplateKey == "index");
        Assert.Equal(1, await db.ThemeSections.CountAsync(s => s.ThemeTemplateId == copiedTpl.ThemeTemplateId && s.SectionType == "Hero"));
    }

    [Fact]
    public async Task Cannot_delete_the_published_theme()
    {
        var (db, lib) = await SeedPublishedAsync();
        using var _ = db;
        var live = (await lib.ListAsync()).Single();

        await Assert.ThrowsAsync<AppException>(() => lib.DeleteAsync(live.ThemeId));

        // A draft can be deleted.
        var draft = await lib.CreateAsync("Temp");
        await lib.DeleteAsync(draft.ThemeId);
        Assert.DoesNotContain(await lib.ListAsync(), t => t.ThemeId == draft.ThemeId);
    }
}

public class StorefrontThemePreviewTests
{
    [Fact]
    public async Task Preview_token_serves_the_draft_theme_not_the_published_one()
    {
        var db = TestDb.New(tenantId: 1);
        using var _ = db;
        var published = new Theme { Name = "Live", IsActive = true, Status = "Published", PreviewToken = "pub", CreatedAt = DateTime.UtcNow };
        var draft = new Theme { Name = "Draft", IsActive = false, Status = "Draft", PreviewToken = "draft-token", CreatedAt = DateTime.UtcNow };
        db.Themes.AddRange(published, draft);
        await db.SaveChangesAsync();
        db.ThemeSettings.Add(new ThemeSetting { ThemeId = published.ThemeId, SettingKey = "PrimaryColor", SettingValue = "#111", CreatedAt = DateTime.UtcNow });
        db.ThemeSettings.Add(new ThemeSetting { ThemeId = draft.ThemeId, SettingKey = "PrimaryColor", SettingValue = "#eee", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var svc = new StorefrontThemeService(db);

        var live = await svc.GetPublishedBundleAsync();
        Assert.Equal("#111", live.Settings["PrimaryColor"]);

        var preview = await svc.GetPublishedBundleAsync("draft-token");
        Assert.Equal("#eee", preview.Settings["PrimaryColor"]);   // draft served by token

        var unknown = await svc.GetPublishedBundleAsync("nope");  // unknown token → published
        Assert.Equal("#111", unknown.Settings["PrimaryColor"]);
    }
}
