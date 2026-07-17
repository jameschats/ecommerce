using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Cms;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class AiPageTests
{
    private sealed class CannedAi(string response) : IAiService
    {
        public bool Enabled => true;
        public long EstimateCostMicros(AiCompletion completion) => 0;
        public Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
            => Task.FromResult(new AiCompletion(response, 60, 400, "test-model"));
    }

    private const string PageJson = """
    {"sections":[
      {"type":"Hero","title":"Welcome","settings":{"style":"banner","image":""},"blocks":[{"image":"","heading":"About Acme","subheading":"Since 2019","buttonText":"","buttonLink":"#"}]},
      {"type":"RichText","title":"Our story","settings":{"content":"<h2>Our story</h2><p>We began in 2019.</p><script>alert(1)</script>","align":"left"},"blocks":[]},
      {"type":"NotARealType","title":"x","settings":{},"blocks":[]}
    ]}
    """;

    private static AiPageService New(EcommerceDbContext db, string aiResponse)
    {
        db.Plans.Add(new Plan { PlanId = 1, Name = "P", Slug = "p", AiCredits = 100, IsActive = true });
        db.TenantSubscriptions.Add(new TenantSubscription { PlanId = 1, Status = "Trial", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        var credits = new AiCreditService(db, new CannedAi(aiResponse), new HttpContextAccessor());
        return new AiPageService(credits, new CmsService(db));
    }

    [Fact]
    public async Task Generate_creates_a_draft_page_with_valid_sections_and_debits_five()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, PageJson);

        var result = await svc.GenerateAsync(new GeneratePageRequest("About Us", "Our story and contact info."));

        Assert.Equal(2, result.Sections);   // Hero + RichText; the unknown type is dropped
        var page = await db.Pages.FirstAsync(p => p.PageId == result.PageId);
        Assert.False(page.IsPublished);      // draft — never auto-published
        Assert.Equal("Custom", page.Type);
        var sections = await db.PageSections.Where(s => s.PageId == result.PageId).ToListAsync();
        Assert.Equal(2, sections.Count);
        Assert.Contains(sections, s => s.SectionType == "Hero");
        Assert.Contains(sections, s => s.SectionType == "RichText");
        Assert.Contains(await db.AiUsageLogs.ToListAsync(), l => l.Feature == AiCreditPricing.Page && l.Credits == -5);
    }

    [Fact]
    public async Task Generate_sanitizes_richtext_content()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, PageJson);

        var result = await svc.GenerateAsync(new GeneratePageRequest("About", "story"));

        var rich = await db.PageSections.FirstAsync(s => s.PageId == result.PageId && s.SectionType == "RichText");
        Assert.NotNull(rich.Settings);
        Assert.DoesNotContain("<script", rich.Settings!);          // script stripped by the CMS sanitizer
        Assert.Contains("Our story", rich.Settings!);              // real content preserved
    }

    [Fact]
    public async Task Generate_gives_the_new_page_a_unique_slug()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, PageJson);
        var cms = new CmsService(db);
        await cms.CreatePageAsync(new SavePageRequest("About", "about", false, null, null));

        var result = await svc.GenerateAsync(new GeneratePageRequest("About", "story"));

        Assert.NotEqual("about", result.Slug);   // collided → distinct slug
        Assert.StartsWith("about", result.Slug);
    }
}
