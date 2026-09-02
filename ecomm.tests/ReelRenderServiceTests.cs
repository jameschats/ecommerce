using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Features.MarketingStudio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ecomm.tests;

public class ReelRenderServiceTests
{
    private sealed class FakePlans(string aspect = "9:16") : IVideoPlanService
    {
        public Task<VideoOptionsDto> OptionsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<VideoPlan> BuildAsync(VideoPlanRequest req, long? userId, CancellationToken ct = default)
        {
            var scenes = new List<VideoScene>
            {
                new(4, "hero_product", "Hook"), new(6, "zoom_product", "Silk Saree"),
                new(5, "detail_product", "Detail"), new(5, "multiple_product_images", "₹2499"), new(4, "brand_logo", "Shop Now"),
            };
            return Task.FromResult(new VideoPlan("Hook", 24, aspect, "festive", "Elegant narration.", scenes));
        }
    }

    private sealed class FakeCatalog(string? img) : ICatalogReader
    {
        public Task<IReadOnlyList<CatalogProduct>> TopProductsAsync(int count, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string?> ProductNameAsync(long id, CancellationToken ct = default) => Task.FromResult<string?>("Silk Saree");
        public Task<CatalogProduct?> GetAsync(long id, CancellationToken ct = default) => Task.FromResult<CatalogProduct?>(new CatalogProduct(id, "Silk Saree", 2499m, img));
    }

    private sealed class FakeBrand(string? logo) : IMarketingBrandService
    {
        public Task<MarketingBrandDto> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new MarketingBrandDto("Acme", null, logo, "#111827", "#6b7280", "#2563eb", "Poppins", true, true, null, null, null, null, null, null, null));
        public Task<MarketingBrandDto> SaveAsync(MarketingBrandDto req, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeQueue : IReelRenderQueue { public long? Last; public void Enqueue(long jobId) => Last = jobId; }

    private static ReelRenderService New(EcommerceDbContext db, FakeQueue queue, string? productImg = "https://cdn/p.jpg", string? logo = "https://cdn/logo.png") =>
        new(db, new FakePlans(), new FakeCatalog(productImg), new FakeBrand(logo),
            tts: null!, music: null!, media: null!, httpFactory: null!, tenant: null!,
            new ConfigurationBuilder().Build(), queue, NullLogger<ReelRenderService>.Instance);

    [Fact]
    public async Task Enqueue_creates_a_queued_job_with_scenes_and_queues_it()
    {
        using var db = TestDb.New(tenantId: 1);
        var queue = new FakeQueue();
        var id = await New(db, queue).EnqueueAsync(new RenderReelRequest(3, "festival", "instagram", "hi-IN", true), userId: 1);

        var job = await db.MarketingVideoRenders.FirstAsync();
        Assert.Equal("queued", job.Status);
        Assert.Equal(1080, job.Width);
        Assert.Equal(1920, job.Height);              // 9:16
        Assert.Contains("Shop Now", job.ScenesJson);
        Assert.Contains("cdn/logo.png", job.ScenesJson);   // brand_logo scene → logo
        Assert.Contains("cdn/p.jpg", job.ScenesJson);      // product scenes → product image
        Assert.True(job.IncludeMusic);
        Assert.Equal(id, queue.Last);                // handed to the background queue
    }

    [Fact]
    public async Task Enqueue_falls_back_to_the_logo_when_the_product_has_no_image()
    {
        using var db = TestDb.New(tenantId: 1);
        await New(db, new FakeQueue(), productImg: null, logo: "https://cdn/logo.png").EnqueueAsync(new RenderReelRequest(3, "sale", "facebook", null, false), null);
        var job = await db.MarketingVideoRenders.FirstAsync();
        Assert.Contains("cdn/logo.png", job.ScenesJson);   // every scene uses the logo
        Assert.DoesNotContain("cdn/p.jpg", job.ScenesJson);
    }

    [Fact]
    public async Task Enqueue_requires_at_least_one_image()
    {
        using var db = TestDb.New(tenantId: 1);
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            New(db, new FakeQueue(), productImg: null, logo: null).EnqueueAsync(new RenderReelRequest(3, "sale", "instagram", null, false), null));
        Assert.Equal(400, ex.StatusCode);
    }
}
