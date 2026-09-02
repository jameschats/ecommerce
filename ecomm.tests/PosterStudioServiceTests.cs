using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Growth;
using ecomm.api.Features.Media;
using ecomm.api.Features.MarketingStudio;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class PosterStudioServiceTests
{
    private sealed class FakeRenderer : IPosterRenderer
    {
        public int Calls;
        public PosterSpec? LastSpec;
        public Task<string> RenderSvgAsync(PosterSpec spec, CancellationToken ct = default)
        { Calls++; LastSpec = spec; return Task.FromResult($"<svg data-headline=\"{spec.Headline}\" data-cta=\"{spec.Cta}\"/>"); }
    }

    private sealed class FakeBrand : IMarketingBrandService
    {
        public Task<MarketingBrandDto> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new MarketingBrandDto("Acme", null, "https://cdn/logo.png", "#111827", "#6b7280", "#2563eb", "Poppins", true, true, null, null, null, null, null, null, null));
        public Task<MarketingBrandDto> SaveAsync(MarketingBrandDto req, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeCatalog : ICatalogReader
    {
        public Task<IReadOnlyList<CatalogProduct>> TopProductsAsync(int count, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string?> ProductNameAsync(long id, CancellationToken ct = default) => Task.FromResult<string?>("Silk Saree");
        public Task<CatalogProduct?> GetAsync(long id, CancellationToken ct = default) =>
            Task.FromResult<CatalogProduct?>(id == 3 ? new CatalogProduct(3, "Silk Saree", 2499m, "https://cdn/saree.jpg") : null);
    }

    private sealed class FakeCopywriter : IMarketingCopywriter
    {
        public int Calls;
        public string Response = "Elegance for every celebration. Shop now!";
        public Task<string> WriteAsync(string kind, long? productId, string brief, long? userId, CancellationToken ct = default)
        { Calls++; return Task.FromResult(Response); }
    }

    private sealed class FakeMedia : IMediaStorage
    {
        public Task<StoredFile> SaveAsync(Stream data, string originalName, string contentType, CancellationToken ct = default) =>
            Task.FromResult(new StoredFile($"https://cdn.test/{originalName}", originalName, data.Length));
        public Task<bool> SaveVariantAsync(string u, string s, Stream d, CancellationToken ct = default) => Task.FromResult(false);
        public Task<Stream?> OpenReadAsync(string url, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
    }

    private sealed class FakeGrowthImages : IGrowthImageService
    {
        public int Calls;
        public GenerateImageRequest? LastRequest;
        public IReadOnlyList<ImageStyleDto> Styles() => throw new NotImplementedException();
        public IReadOnlyList<ImageFormatDto> Formats() => throw new NotImplementedException();
        public Task<GeneratedImageDto> GenerateAsync(GenerateImageRequest req, long? userId, CancellationToken ct = default)
        { Calls++; LastRequest = req; return Task.FromResult(new GeneratedImageDto(1, "https://cdn.test/product-bg.png", 4.5m, DateTime.UtcNow)); }
        public Task<IReadOnlyList<GeneratedImageDto>> RecentAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeCreditService : IAiCreditService
    {
        public int Calls;
        public Task<AiBalanceDto> GetBalanceAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AiUsageDto>> GetUsageAsync(int take = 50, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ecomm.api.Data.Entities.AiCreditPack?> GetPackAsync(int packId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> TopUpAsync(int packId, string reference, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<T> MeterAsync<T>(string feature, Func<IAiService, Task<(T Result, AiCompletion Usage)>> action, CancellationToken ct = default) => throw new NotImplementedException();
        public async Task<T> MeterImageAsync<T>(string feature, Func<IImageAiService, Task<(T Result, ImageResult Usage)>> action, CancellationToken ct = default)
        {
            Calls++;
            var (result, _) = await action(new FakeImageAi());
            return result;
        }
    }

    private sealed class FakeImageAi : IImageAiService
    {
        public bool Enabled => true;
        public Task<ImageResult> GenerateAsync(ImagePrompt prompt, CancellationToken ct = default) =>
            Task.FromResult(new ImageResult(new byte[] { 1, 2, 3 }, "image/png", 4_500_000, "gpt-image-1"));
    }

    private static PosterStudioService New(
        EcommerceDbContext db, FakeRenderer? renderer = null, FakeCopywriter? copy = null,
        FakeGrowthImages? growthImages = null, FakeCreditService? credits = null) =>
        new(db, renderer ?? new FakeRenderer(), new FakeBrand(), new FakeCatalog(), copy ?? new FakeCopywriter(), new FakeMedia(),
            growthImages ?? new FakeGrowthImages(), credits ?? new FakeCreditService());

    [Fact]
    public async Task Preview_renders_without_touching_the_database_or_spending_credits()
    {
        using var db = TestDb.New(tenantId: 1);
        var renderer = new FakeRenderer();
        var copy = new FakeCopywriter();

        var result = await New(db, renderer, copy).PreviewAsync(
            new PosterStudioRequest("org", null, "Big Diwali Sale", 999m, "Shop Now", true, true));

        Assert.Contains("Big Diwali Sale", result.Svg);
        Assert.Equal(1, renderer.Calls);
        Assert.Equal(0, copy.Calls);                 // free — no AI call
        Assert.Empty(db.MarketingCreatives);          // and nothing persisted
        Assert.Empty(db.MarketingPlanItems);
    }

    [Fact]
    public async Task Preview_uses_the_product_photo_for_a_product_poster()
    {
        using var db = TestDb.New(tenantId: 1);
        var renderer = new FakeRenderer();

        await New(db, renderer).PreviewAsync(new PosterStudioRequest("product", 3, "Spotlight", null, "Buy Now", true, false));

        Assert.Equal("https://cdn/saree.jpg", renderer.LastSpec!.ProductImageUrl);
    }

    [Fact]
    public async Task Preview_lets_the_merchant_hide_the_price_even_when_the_product_has_one()
    {
        using var db = TestDb.New(tenantId: 1);
        var renderer = new FakeRenderer();

        await New(db, renderer).PreviewAsync(new PosterStudioRequest("product", 3, "Spotlight", null, "Buy Now", true, false));

        Assert.Null(renderer.LastSpec!.Price);        // request's explicit null wins over the product's ₹2499
    }

    [Fact]
    public async Task Preview_requires_a_product_for_a_product_poster()
    {
        using var db = TestDb.New(tenantId: 1);
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            New(db).PreviewAsync(new PosterStudioRequest("product", null, "Spotlight", null, "Buy Now", true, true)));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Create_persists_a_creative_and_an_adhoc_item_and_spends_one_credit()
    {
        using var db = TestDb.New(tenantId: 1);
        var copy = new FakeCopywriter();

        var result = await New(db, copy: copy).CreateAsync(
            new PosterStudioRequest("org", null, "Big Diwali Sale", 999m, "Shop Now", true, true), userId: 5);

        Assert.Equal(1, copy.Calls);
        Assert.NotEmpty(result.MediaUrl);
        Assert.Equal(copy.Response, result.Caption);
        Assert.Single(db.MarketingCreatives);
        var item = await db.MarketingPlanItems.FirstAsync();
        Assert.Equal("approved", item.Status);
        Assert.Equal("", item.Channels);              // not scheduled yet — assigned later
    }

    [Fact]
    public async Task Create_reuses_the_same_adhoc_plan_across_multiple_posters()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db);
        await svc.CreateAsync(new PosterStudioRequest("org", null, "First", null, "Shop Now", true, true), null);
        await svc.CreateAsync(new PosterStudioRequest("org", null, "Second", null, "Shop Now", true, true), null);

        Assert.Single(db.MarketingPlans.Where(p => p.Status == "adhoc"));
        Assert.Equal(2, db.MarketingPlanItems.Count());
    }

    [Fact]
    public async Task Create_never_becomes_this_weeks_plan()
    {
        // The ad-hoc bucket must stay invisible to GetCurrentAsync — verified indirectly here by
        // confirming its WeekStart is nowhere near any real computed week.
        using var db = TestDb.New(tenantId: 1);
        await New(db).CreateAsync(new PosterStudioRequest("org", null, "First", null, "Shop Now", true, true), null);
        var plan = await db.MarketingPlans.FirstAsync(p => p.Status == "adhoc");
        Assert.True(plan.WeekStart.Year < 2000);
    }

    [Fact]
    public async Task Suggest_headline_returns_a_short_ai_line()
    {
        using var db = TestDb.New(tenantId: 1);
        var copy = new FakeCopywriter { Response = "Elegance for every celebration.\nShop the new collection." };
        var result = await New(db, copy: copy).SuggestHeadlineAsync(null, "festival sale", CancellationToken.None);
        Assert.Equal(1, copy.Calls);
        Assert.Equal("Elegance for every celebration", result.Headline);
    }

    [Fact]
    public void Background_styles_are_exposed_for_the_picker()
    {
        using var db = TestDb.New(tenantId: 1);
        var styles = New(db).BackgroundStyles();
        Assert.Contains(styles, s => s.Key == "festive");
        Assert.Contains(styles, s => s.Key == "lifestyle");
    }

    [Fact]
    public async Task Generate_background_for_a_product_rides_the_existing_product_image_pipeline()
    {
        using var db = TestDb.New(tenantId: 1);
        var growthImages = new FakeGrowthImages();
        var credits = new FakeCreditService();

        var result = await New(db, growthImages: growthImages, credits: credits).GenerateBackgroundAsync(
            new PosterStudioRequest("product", 3, "Spotlight", null, "Buy Now", true, true), "festive", userId: 9);

        Assert.Equal("https://cdn.test/product-bg.png", result.Url);
        Assert.Equal(1, growthImages.Calls);
        Assert.Equal(0, credits.Calls);                    // product path never touches the org-level metering
        Assert.Equal(3, growthImages.LastRequest!.ProductId);
        Assert.Equal("festive", growthImages.LastRequest.Style);
    }

    [Fact]
    public async Task Generate_background_for_an_organization_meters_a_direct_image_call()
    {
        using var db = TestDb.New(tenantId: 1);
        var growthImages = new FakeGrowthImages();
        var credits = new FakeCreditService();

        var result = await New(db, growthImages: growthImages, credits: credits).GenerateBackgroundAsync(
            new PosterStudioRequest("org", null, "Big Sale", null, "Shop Now", true, true), "studio", null);

        Assert.NotEmpty(result.Url);
        Assert.Equal(0, growthImages.Calls);                // no product to delegate to
        Assert.Equal(1, credits.Calls);
    }

    [Fact]
    public async Task Generate_background_rejects_an_unknown_style()
    {
        using var db = TestDb.New(tenantId: 1);
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            New(db).GenerateBackgroundAsync(new PosterStudioRequest("org", null, "X", null, "Shop Now", true, true), "not-a-style", null));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Preview_composites_over_a_generated_background_when_one_is_set()
    {
        using var db = TestDb.New(tenantId: 1);
        var renderer = new FakeRenderer();
        await New(db, renderer).PreviewAsync(new PosterStudioRequest(
            "org", null, "Big Sale", null, "Shop Now", true, true, BackgroundImageUrl: "https://cdn.test/bg.png"));
        Assert.Equal("https://cdn.test/bg.png", renderer.LastSpec!.BackgroundImageUrl);
    }
}
