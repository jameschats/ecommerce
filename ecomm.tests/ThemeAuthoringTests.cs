using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Storefront;
using Xunit;

namespace ecomm.tests;

public class ThemeAuthoringTests
{
    private static async Task<ecomm.api.Data.Context.EcommerceDbContext> SeedThemeAsync()
    {
        var db = TestDb.New(tenantId: 1);
        db.Themes.Add(new Theme { Name = "Default", IsActive = true, Status = "Published", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Add_section_respects_scope()
    {
        using var db = await SeedThemeAsync();
        var svc = new ThemeAuthoringService(db);

        // ProductInfo is valid on the product template…
        var added = await svc.AddSectionAsync("product", "ProductInfo");
        Assert.Equal("ProductInfo", added.SectionType);
        Assert.Equal("dynamic", added.Kind);

        // …but not on the cart template.
        await Assert.ThrowsAsync<AppException>(() => svc.AddSectionAsync("cart", "ProductInfo"));
        // a static (any-scope) section is allowed on a page template…
        var rich = await svc.AddSectionAsync("cart", "RichText");
        Assert.Equal("RichText", rich.SectionType);
        // …but NOT on a shared group zone (header/footer/announcement).
        await Assert.ThrowsAsync<AppException>(() => svc.AddSectionAsync("header", "RichText"));
        var header = await svc.AddSectionAsync("header", "Header");
        Assert.Equal("Header", header.SectionType);
    }

    [Fact]
    public async Task Add_assigns_incrementing_order_and_default_settings()
    {
        using var db = await SeedThemeAsync();
        var svc = new ThemeAuthoringService(db);

        var a = await svc.AddSectionAsync("index", "Hero");
        var b = await svc.AddSectionAsync("index", "RichText");
        Assert.Equal(1, a.DisplayOrder);
        Assert.Equal(2, b.DisplayOrder);
        Assert.Contains("autoplay", a.Settings);   // Hero defaults seeded from the registry schema

        var sections = await svc.GetSectionsAsync("index");
        Assert.Equal(2, sections.Count);
    }

    [Fact]
    public async Task Update_sanitizes_richtext_settings()
    {
        using var db = await SeedThemeAsync();
        var svc = new ThemeAuthoringService(db);
        var s = await svc.AddSectionAsync("index", "RichText");

        var updated = await svc.UpdateSectionAsync(s.Id, new SaveThemeSectionRequest(
            "Intro", "{\"content\":\"<p>hi</p><script>x()</script>\",\"align\":\"center\"}", "[]", true, null, null));

        Assert.Contains("<p>hi</p>", updated.Settings);
        Assert.DoesNotContain("<script", updated.Settings);
    }

    [Fact]
    public async Task Reorder_sets_display_order()
    {
        using var db = await SeedThemeAsync();
        var svc = new ThemeAuthoringService(db);
        var a = await svc.AddSectionAsync("index", "Hero");
        var b = await svc.AddSectionAsync("index", "RichText");

        await svc.ReorderSectionsAsync("index", new() { b.Id, a.Id });

        var sections = await svc.GetSectionsAsync("index");
        Assert.Equal(b.Id, sections[0].Id);   // b now first
        Assert.Equal(a.Id, sections[1].Id);
    }

    [Fact]
    public async Task Delete_and_duplicate_work()
    {
        using var db = await SeedThemeAsync();
        var svc = new ThemeAuthoringService(db);
        var a = await svc.AddSectionAsync("index", "Hero");

        var dup = await svc.DuplicateSectionAsync(a.Id);
        Assert.Equal("Hero", dup.SectionType);
        Assert.Equal(2, (await svc.GetSectionsAsync("index")).Count);

        await svc.DeleteSectionAsync(a.Id);
        Assert.Single(await svc.GetSectionsAsync("index"));
    }

    [Fact]
    public async Task List_templates_reports_section_counts()
    {
        using var db = await SeedThemeAsync();
        var svc = new ThemeAuthoringService(db);
        await svc.AddSectionAsync("index", "Hero");

        var templates = await svc.ListTemplatesAsync();
        var index = templates.First(t => t.TemplateKey == "index");
        Assert.Equal(1, index.SectionCount);
        Assert.Equal("Home", index.Label);
        Assert.Contains(templates, t => t.TemplateKey == "product" && t.SectionCount == 0);
    }
}
