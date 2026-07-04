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
}
