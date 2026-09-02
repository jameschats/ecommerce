using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Media;
using ecomm.api.Features.MarketingStudio;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class MarketingGenerationServiceTests
{
    private sealed class FakeCopywriter : IMarketingCopywriter
    {
        public int Calls;
        public Task<string> WriteAsync(string kind, long? productId, string brief, long? userId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult($"[{kind}] {brief}");
        }
    }

    private sealed class FakeBrand : IMarketingBrandService
    {
        public Task<MarketingBrandDto> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new MarketingBrandDto("Acme", null, null, "#111827", "#6b7280", "#2563eb", "Poppins", true, true, null, null, null, null, null, null, null));
        public Task<MarketingBrandDto> SaveAsync(MarketingBrandDto req, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeCatalog : ICatalogReader
    {
        public Task<IReadOnlyList<CatalogProduct>> TopProductsAsync(int count, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CatalogProduct>>(new[] { new CatalogProduct(3, "Silk Saree", 2499m, null) });
        public Task<string?> ProductNameAsync(long id, CancellationToken ct = default) => Task.FromResult<string?>("Silk Saree");
        public Task<CatalogProduct?> GetAsync(long id, CancellationToken ct = default) => Task.FromResult<CatalogProduct?>(new CatalogProduct(id, "Silk Saree", 2499m, null));
    }

    private sealed class FakePoster : IPosterRenderer
    {
        public int Calls;
        public Task<string> RenderSvgAsync(PosterSpec spec, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult($"<svg data-headline=\"{spec.Headline}\"/>");
        }
    }

    private sealed class FakeMedia : IMediaStorage
    {
        public Task<StoredFile> SaveAsync(Stream data, string originalName, string contentType, CancellationToken ct = default) =>
            Task.FromResult(new StoredFile($"https://cdn.test/{originalName}", originalName, data.Length));
        public Task<bool> SaveVariantAsync(string originalUrl, string suffix, Stream data, CancellationToken ct = default) => Task.FromResult(false);
        public Task<Stream?> OpenReadAsync(string url, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
    }

    private static async Task<long> SeedPlanAsync(EcommerceDbContext db, params MarketingPlanItem[] items)
    {
        var plan = new MarketingPlan { WeekStart = DateTime.UtcNow.Date, Status = "confirmed", CreatedAt = DateTime.UtcNow };
        db.MarketingPlans.Add(plan);
        await db.SaveChangesAsync();
        foreach (var it in items) { it.MarketingPlanId = plan.MarketingPlanId; it.CreatedAt = DateTime.UtcNow; db.MarketingPlanItems.Add(it); }
        await db.SaveChangesAsync();
        return plan.MarketingPlanId;
    }

    private static MarketingPlanItem TextItem(string channels, string topic = "Topic") =>
        new() { Type = "text", Topic = topic, Channels = channels, Status = "proposed", ScheduledAt = DateTime.UtcNow.Date.AddHours(10) };

    private static MarketingGenerationService New(EcommerceDbContext db, IMarketingCopywriter copy, FakePoster? poster = null, FakeMedia? media = null) =>
        new(db, copy, new FakeBrand(), new FakeCatalog(), poster ?? new FakePoster(), media ?? new FakeMedia());

    [Fact]
    public async Task Generates_one_creative_per_item_and_fans_out_a_post_per_channel()
    {
        using var db = TestDb.New(tenantId: 1);
        var planId = await SeedPlanAsync(db, TextItem("linkedin,instagram"), TextItem("whatsapp"));
        var copy = new FakeCopywriter();

        var result = await New(db, copy).GenerateForPlanAsync(planId, userId: 7);

        Assert.Equal(2, result.CreativesGenerated);
        Assert.Equal(3, result.PostsScheduled);                 // 2 + 1 channels
        Assert.Equal(2, copy.Calls);                            // one caption per item (reused across channels)
        Assert.Equal(2, db.MarketingCreatives.Count());
        Assert.Equal(3, db.ScheduledPosts.Count());
        Assert.All(db.ScheduledPosts, p => Assert.Equal("pending_approval", p.Status));
    }

    [Fact]
    public async Task Marks_generated_items_approved_and_is_idempotent()
    {
        using var db = TestDb.New(tenantId: 1);
        var planId = await SeedPlanAsync(db, TextItem("linkedin"));
        var svc = New(db, new FakeCopywriter());

        await svc.GenerateForPlanAsync(planId, null);
        var second = await svc.GenerateForPlanAsync(planId, null);   // nothing left to generate

        Assert.Equal(0, second.CreativesGenerated);
        Assert.Equal("approved", (await db.MarketingPlanItems.FirstAsync()).Status);
        Assert.Single(db.MarketingCreatives);
    }

    [Fact]
    public async Task Generates_content_with_no_channel_but_leaves_it_unscheduled()
    {
        // The merchant should be able to see what the AI made before connecting any channel — only the
        // ScheduledPost fan-out (nowhere to post to) is skipped, not the generation itself.
        using var db = TestDb.New(tenantId: 1);
        var planId = await SeedPlanAsync(db, TextItem(""));
        var copy = new FakeCopywriter();

        var result = await New(db, copy).GenerateForPlanAsync(planId, null);

        Assert.Equal(1, result.CreativesGenerated);
        Assert.Equal(1, result.Unscheduled);
        Assert.Equal(0, result.PostsScheduled);
        Assert.Equal(1, copy.Calls);                       // still spends a credit — content was made
        Assert.Empty(db.ScheduledPosts);
        Assert.Equal("approved", (await db.MarketingPlanItems.FirstAsync()).Status);
    }

    [Fact]
    public async Task Schedule_existing_assigns_channels_to_an_already_generated_item_without_regenerating()
    {
        using var db = TestDb.New(tenantId: 1);
        var planId = await SeedPlanAsync(db, TextItem(""));
        var copy = new FakeCopywriter();
        var svc = New(db, copy);
        await svc.GenerateForPlanAsync(planId, null);
        var itemId = (await db.MarketingPlanItems.FirstAsync()).MarketingPlanItemId;

        var result = await svc.ScheduleExistingAsync(itemId, new[] { "linkedin", "instagram" });

        Assert.Equal(2, result.PostsScheduled);
        Assert.Equal(1, copy.Calls);                        // no re-generation, no extra credit spend
        Assert.Equal(2, db.ScheduledPosts.Count());
        Assert.All(db.ScheduledPosts, p => Assert.Equal("pending_approval", p.Status));
    }

    [Fact]
    public async Task Schedule_existing_is_idempotent_for_channels_already_scheduled()
    {
        using var db = TestDb.New(tenantId: 1);
        var planId = await SeedPlanAsync(db, TextItem("linkedin"));
        var svc = New(db, new FakeCopywriter());
        await svc.GenerateForPlanAsync(planId, null);
        var itemId = (await db.MarketingPlanItems.FirstAsync()).MarketingPlanItemId;

        var result = await svc.ScheduleExistingAsync(itemId, new[] { "linkedin", "pinterest" });

        Assert.Equal(1, result.PostsScheduled);             // linkedin already scheduled; only pinterest added
        Assert.Equal(2, db.ScheduledPosts.Count());
    }

    [Fact]
    public async Task Schedule_existing_requires_the_item_to_have_been_generated()
    {
        using var db = TestDb.New(tenantId: 1);
        var planId = await SeedPlanAsync(db, TextItem(""));   // never generated
        var itemId = (await db.MarketingPlanItems.FirstAsync()).MarketingPlanItemId;

        var ex = await Assert.ThrowsAsync<ecomm.api.Common.Exceptions.AppException>(
            () => New(db, new FakeCopywriter()).ScheduleExistingAsync(itemId, new[] { "linkedin" }));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Renders_a_poster_creative_with_media_url_and_a_caption()
    {
        using var db = TestDb.New(tenantId: 1);
        var poster = new MarketingPlanItem { Type = "poster", Topic = "Festival offer", ProductId = 3, Channels = "instagram", Status = "proposed", ScheduledAt = DateTime.UtcNow };
        var planId = await SeedPlanAsync(db, poster);
        var rendered = new FakePoster();

        var result = await New(db, new FakeCopywriter(), rendered).GenerateForPlanAsync(planId, null);

        Assert.Equal(1, result.CreativesGenerated);
        Assert.Equal(1, result.PostsScheduled);
        Assert.Equal(1, rendered.Calls);
        var creative = Assert.Single(db.MarketingCreatives);
        Assert.Equal("poster", creative.Type);
        Assert.False(string.IsNullOrEmpty(creative.OutputMediaUrl));   // stored SVG
        Assert.False(string.IsNullOrEmpty(creative.Body));             // caption
    }

    [Fact]
    public async Task Skips_video_items_until_MS3()
    {
        using var db = TestDb.New(tenantId: 1);
        var video = new MarketingPlanItem { Type = "video", Topic = "V", Channels = "instagram", Status = "proposed", ScheduledAt = DateTime.UtcNow };
        var planId = await SeedPlanAsync(db, video);

        var result = await New(db, new FakeCopywriter()).GenerateForPlanAsync(planId, null);

        Assert.Equal(0, result.CreativesGenerated);
        Assert.Equal(1, result.Skipped);
    }
}
