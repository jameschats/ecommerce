using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Cms;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class CmsBuilderTests
{
    private static async Task<(EcommerceDbContext db, CmsService svc)> SeedRichTextAsync()
    {
        var db = TestDb.New(tenantId: 1);
        db.Pages.Add(new Page { PageId = 1, Slug = "p", Title = "P", Type = "Custom", IsPublished = true });
        db.PageSections.Add(new PageSection { PageSectionId = 1, PageId = 1, SectionType = "RichText" });
        await db.SaveChangesAsync();
        return (db, new CmsService(db));
    }

    [Fact]
    public async Task RichText_html_is_sanitized_on_save()
    {
        var (db, svc) = await SeedRichTextAsync();
        using var _ = db;

        var dirty = "{\"content\":\"<p>ok</p><script>alert(1)</script><a href=\\\"javascript:evil()\\\" onclick=\\\"steal()\\\">x</a>\",\"align\":\"left\"}";
        await svc.UpdateSectionAsync(1, new SaveSectionRequest(null, dirty, null, true, null, null), default);

        var saved = await db.PageSections.IgnoreQueryFilters().SingleAsync();
        Assert.DoesNotContain("<script", saved.Settings);
        Assert.DoesNotContain("onclick", saved.Settings);
        Assert.DoesNotContain("javascript:", saved.Settings);
        Assert.Contains("<p>ok</p>", saved.Settings);   // safe content kept
    }

    [Fact]
    public async Task Unknown_section_type_is_rejected()
    {
        var (db, svc) = await SeedRichTextAsync();
        using var _ = db;
        await Assert.ThrowsAsync<ecomm.api.Common.Exceptions.AppException>(
            () => svc.AddSectionAsync(new AddSectionRequest(1, "NotARealType"), default));
    }

    [Fact]
    public async Task Add_section_with_default_settings_serializes()
    {
        // Regression: adding a section that has default settings (Hero → autoplay/intervalSec)
        // used to throw at ToJsonString because JsonValue.Create((object)…) needs a TypeInfoResolver.
        var (db, svc) = await SeedRichTextAsync();
        using var _ = db;

        var section = await svc.AddSectionAsync(new AddSectionRequest(1, "Hero"), default);

        Assert.Equal("Hero", section.SectionType);
        Assert.Contains("autoplay", section.Settings);
        Assert.Contains("intervalSec", section.Settings);
    }

    [Fact]
    public async Task ApplyPreset_replaces_sections_with_valid_types()
    {
        var (db, svc) = await SeedRichTextAsync();   // page 1 starts with one RichText section
        using var _ = db;

        var detail = await svc.ApplyPresetAsync(1, "fashion", default);

        Assert.NotEmpty(detail.Sections);
        // Old section is gone; only preset sections remain.
        Assert.Equal(detail.Sections.Count, await db.PageSections.IgnoreQueryFilters().CountAsync(s => s.PageId == 1));
        // Every section is a valid catalog type, ordered 1..n.
        Assert.All(detail.Sections, s => Assert.True(
            ecomm.api.Features.Cms.SectionTypes.SectionTypeRegistry.IsValidType(s.SectionType)));
        Assert.Equal(Enumerable.Range(1, detail.Sections.Count), detail.Sections.Select(s => s.DisplayOrder));
    }

    [Fact]
    public async Task ApplyPreset_unknown_key_is_rejected()
    {
        var (db, svc) = await SeedRichTextAsync();
        using var _ = db;
        await Assert.ThrowsAsync<ecomm.api.Common.Exceptions.AppException>(
            () => svc.ApplyPresetAsync(1, "not-an-industry", default));
    }
}
