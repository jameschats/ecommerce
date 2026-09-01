using ecomm.api.Features.MarketingStudio;
using Xunit;

namespace ecomm.tests;

public class VideoPlanServiceTests
{
    private sealed class FakeCopywriter(string? script = null, bool throws = false) : IMarketingCopywriter
    {
        public int Calls;
        public Task<string> WriteAsync(string kind, long? productId, string brief, long? userId, CancellationToken ct = default)
        {
            Calls++;
            if (throws) throw new InvalidOperationException("no credits");
            return Task.FromResult(script ?? "Discover something special. Shop now!");
        }
    }

    private sealed class FakeCatalog(bool withProduct) : ICatalogReader
    {
        public Task<IReadOnlyList<CatalogProduct>> TopProductsAsync(int count, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CatalogProduct>>(new[] { new CatalogProduct(3, "Silk Saree", 2499m, null) });
        public Task<string?> ProductNameAsync(long id, CancellationToken ct = default) => Task.FromResult<string?>("Silk Saree");
        public Task<CatalogProduct?> GetAsync(long id, CancellationToken ct = default) =>
            Task.FromResult(withProduct ? new CatalogProduct(id, "Silk Saree", 2499m, null) : null);
    }

    private sealed class FakeBrand : IMarketingBrandService
    {
        public Task<MarketingBrandDto> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new MarketingBrandDto("Meenakshi Silks", null, null, "#111827", "#6b7280", "#2563eb", "Poppins", true, true, null, null, null, null, null, null, null));
        public Task<MarketingBrandDto> SaveAsync(MarketingBrandDto req, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private static VideoPlanService New(IMarketingCopywriter? copy = null, bool withProduct = true) =>
        new(copy ?? new FakeCopywriter(), new FakeCatalog(withProduct), new FakeBrand());

    [Fact]
    public async Task Options_lists_goals_platforms_and_products()
    {
        var opt = await New().OptionsAsync();
        Assert.Contains(opt.Goals, g => g.Code == "festival");
        Assert.Contains(opt.Platforms, p => p.Code == "instagram");
        Assert.Contains(opt.Products, p => p.Id == 3);
    }

    [Fact]
    public async Task Build_produces_five_scenes_with_narration_and_reel_aspect()
    {
        var copy = new FakeCopywriter("Elegance for every celebration. Shop now.");
        var plan = await New(copy).BuildAsync(new VideoPlanRequest(3, "festival", "instagram"), userId: 1);

        Assert.Equal(5, plan.Scenes.Count);
        Assert.Equal("9:16", plan.Aspect);                       // reel
        Assert.Equal(24, plan.DurationSeconds);                  // 4+6+5+5+4
        Assert.Equal("Celebrate the season", plan.Hook);         // festival hook
        Assert.Contains("Elegance", plan.Narration);
        Assert.Equal(1, copy.Calls);                             // AI wrote the narration
        Assert.Contains(plan.Scenes, s => s.Text == "₹2499");    // price caption
        Assert.Contains(plan.Scenes, s => s.Text == "Shop Now"); // CTA
    }

    [Fact]
    public async Task Build_falls_back_to_a_local_script_when_the_writer_fails()
    {
        var plan = await New(new FakeCopywriter(throws: true)).BuildAsync(new VideoPlanRequest(3, "sale", "facebook"), null);
        Assert.False(string.IsNullOrWhiteSpace(plan.Narration));  // deterministic fallback
        Assert.Equal("1:1", plan.Aspect);                         // facebook
        Assert.Contains("Silk Saree", plan.Narration);
    }

    [Fact]
    public async Task Build_without_a_product_uses_the_company_as_the_subject()
    {
        var plan = await New(withProduct: false).BuildAsync(new VideoPlanRequest(null, "brand-story", "linkedin"), null);
        Assert.Contains(plan.Scenes, s => s.Text == "Meenakshi Silks");
        Assert.Contains(plan.Scenes, s => s.Text == "Great value");   // no price → value caption
    }
}
