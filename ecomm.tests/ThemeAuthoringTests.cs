using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Storefront;
using Xunit;

namespace ecomm.tests;

public class ThemeAuthoringTests
{
    private static async Task<(ecomm.api.Data.Context.EcommerceDbContext db, ThemeAuthoringService svc, long themeId)> SeedAsync()
    {
        var db = TestDb.New(tenantId: 1);
        var theme = new Theme { Name = "Default", IsActive = true, Status = "Published", CreatedAt = DateTime.UtcNow };
        db.Themes.Add(theme);
        await db.SaveChangesAsync();
        return (db, new ThemeAuthoringService(db), theme.ThemeId);
    }

    [Fact]
    public async Task Add_section_respects_scope()
    {
        var (db, svc, id) = await SeedAsync();
        using var _ = db;

        var added = await svc.AddSectionAsync(id, "product", "ProductInfo");
        Assert.Equal("ProductInfo", added.SectionType);
        Assert.Equal("dynamic", added.Kind);

        await Assert.ThrowsAsync<AppException>(() => svc.AddSectionAsync(id, "cart", "ProductInfo"));
        var rich = await svc.AddSectionAsync(id, "cart", "RichText");
        Assert.Equal("RichText", rich.SectionType);

        // group zones only accept their own section
        await Assert.ThrowsAsync<AppException>(() => svc.AddSectionAsync(id, "header", "RichText"));
        var header = await svc.AddSectionAsync(id, "header", "Header");
        Assert.Equal("Header", header.SectionType);
    }

    [Fact]
    public async Task Add_assigns_incrementing_order_and_default_settings()
    {
        var (db, svc, id) = await SeedAsync();
        using var _ = db;

        var a = await svc.AddSectionAsync(id, "index", "Hero");
        var b = await svc.AddSectionAsync(id, "index", "RichText");
        Assert.Equal(1, a.DisplayOrder);
        Assert.Equal(2, b.DisplayOrder);
        Assert.Contains("autoplay", a.Settings);

        Assert.Equal(2, (await svc.GetSectionsAsync(id, "index")).Count);
    }

    [Fact]
    public async Task Update_sanitizes_richtext_settings()
    {
        var (db, svc, id) = await SeedAsync();
        using var _ = db;
        var s = await svc.AddSectionAsync(id, "index", "RichText");

        var updated = await svc.UpdateSectionAsync(s.Id, new SaveThemeSectionRequest(
            "Intro", "{\"content\":\"<p>hi</p><script>x()</script>\",\"align\":\"center\"}", "[]", true, null, null));

        Assert.Contains("<p>hi</p>", updated.Settings);
        Assert.DoesNotContain("<script", updated.Settings);
    }

    [Fact]
    public async Task Reorder_sets_display_order()
    {
        var (db, svc, id) = await SeedAsync();
        using var _ = db;
        var a = await svc.AddSectionAsync(id, "index", "Hero");
        var b = await svc.AddSectionAsync(id, "index", "RichText");

        await svc.ReorderSectionsAsync(id, "index", new() { b.Id, a.Id });

        var sections = await svc.GetSectionsAsync(id, "index");
        Assert.Equal(b.Id, sections[0].Id);
        Assert.Equal(a.Id, sections[1].Id);
    }

    [Fact]
    public async Task Delete_and_duplicate_work()
    {
        var (db, svc, id) = await SeedAsync();
        using var _ = db;
        var a = await svc.AddSectionAsync(id, "index", "Hero");

        var dup = await svc.DuplicateSectionAsync(a.Id);
        Assert.Equal("Hero", dup.SectionType);
        Assert.Equal(2, (await svc.GetSectionsAsync(id, "index")).Count);

        await svc.DeleteSectionAsync(a.Id);
        Assert.Single(await svc.GetSectionsAsync(id, "index"));
    }

    [Fact]
    public async Task List_templates_reports_section_counts()
    {
        var (db, svc, id) = await SeedAsync();
        using var _ = db;
        await svc.AddSectionAsync(id, "index", "Hero");

        var templates = await svc.ListTemplatesAsync(id);
        var index = templates.First(t => t.TemplateKey == "index");
        Assert.Equal(1, index.SectionCount);
        Assert.Equal("Home", index.Label);
        Assert.Contains(templates, t => t.TemplateKey == "product" && t.SectionCount == 0);
    }

    [Fact]
    public async Task Authoring_a_missing_theme_is_rejected()
    {
        var (db, svc, _) = await SeedAsync();
        using var _2 = db;
        await Assert.ThrowsAsync<AppException>(() => svc.AddSectionAsync(9999, "index", "Hero"));
    }
}
