using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
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
        public IReadOnlyList<string> AvailableFonts { get; } = new[] { "Poppins", "Oswald" };
        public IReadOnlyList<PosterTemplateInfo> AvailableTemplates { get; } = new[]
        {
            new PosterTemplateInfo("bold-medallion", "Bold Medallion", "d", true, "Product Spotlight"),
            new PosterTemplateInfo("minimal-type", "Minimal Type", "d", false, "Sale & Offer"),
        };
        public IReadOnlyList<PosterFormatInfo> AvailableFormats { get; } = new[]
        {
            new PosterFormatInfo("square", "Square", 1080, 1080), new PosterFormatInfo("story", "Story", 1080, 1920),
        };
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

    [Fact]
    public async Task Preview_passes_through_the_chosen_template_font_and_scale()
    {
        using var db = TestDb.New(tenantId: 1);
        var renderer = new FakeRenderer();
        await New(db, renderer).PreviewAsync(new PosterStudioRequest(
            "org", null, "Big Sale", null, "Shop Now", true, true,
            TemplateId: "minimal-type", Font: "Oswald", HeadlineScale: "large"));

        Assert.Equal("minimal-type", renderer.LastSpec!.TemplateId);
        Assert.Equal("Oswald", renderer.LastSpec.HeadlineFont);
        Assert.Equal("large", renderer.LastSpec.HeadlineScale);
    }

    [Fact]
    public async Task Preview_defaults_template_and_scale_when_not_specified()
    {
        using var db = TestDb.New(tenantId: 1);
        var renderer = new FakeRenderer();
        await New(db, renderer).PreviewAsync(new PosterStudioRequest("org", null, "Big Sale", null, "Shop Now", true, true));

        Assert.Equal("bold-medallion", renderer.LastSpec!.TemplateId);
        Assert.Equal("medium", renderer.LastSpec.HeadlineScale);
        Assert.Null(renderer.LastSpec.HeadlineFont);   // falls back to the brand kit's font inside the renderer
    }

    [Fact]
    public void Options_surfaces_templates_fonts_and_background_styles()
    {
        using var db = TestDb.New(tenantId: 1);
        var opts = New(db, new FakeRenderer()).Options();
        Assert.Equal(2, opts.Templates.Count);
        Assert.Contains("Poppins", opts.Fonts);
        Assert.NotEmpty(opts.BackgroundStyles);
        Assert.Equal(2, opts.Formats.Count);
        Assert.Contains(opts.Formats, f => f.Id == "story");
    }

    [Fact]
    public async Task Preview_passes_through_the_chosen_format()
    {
        using var db = TestDb.New(tenantId: 1);
        var renderer = new FakeRenderer();
        await New(db, renderer).PreviewAsync(new PosterStudioRequest(
            "org", null, "Big Sale", null, "Shop Now", true, true, Format: "story"));

        Assert.Equal("story", renderer.LastSpec!.Format);
    }

    [Fact]
    public async Task Preview_defaults_to_square_when_format_not_specified()
    {
        using var db = TestDb.New(tenantId: 1);
        var renderer = new FakeRenderer();
        await New(db, renderer).PreviewAsync(new PosterStudioRequest("org", null, "Big Sale", null, "Shop Now", true, true));

        Assert.Equal("square", renderer.LastSpec!.Format);
    }

    [Fact]
    public async Task Preview_lets_a_poster_override_brand_colours()
    {
        using var db = TestDb.New(tenantId: 1);
        var renderer = new FakeRenderer();
        await New(db, renderer).PreviewAsync(new PosterStudioRequest(
            "org", null, "Big Sale", null, "Shop Now", true, true,
            PrimaryColor: "#ff0000", SecondaryColor: "#00ff00", AccentColor: "#0000ff"));

        Assert.Equal("#ff0000", renderer.LastSpec!.Primary);
        Assert.Equal("#00ff00", renderer.LastSpec.Secondary);
        Assert.Equal("#0000ff", renderer.LastSpec.Accent);
    }

    [Fact]
    public async Task Preview_falls_back_to_brand_colours_when_no_override_given()
    {
        using var db = TestDb.New(tenantId: 1);
        var renderer = new FakeRenderer();
        await New(db, renderer).PreviewAsync(new PosterStudioRequest("org", null, "Big Sale", null, "Shop Now", true, true));

        Assert.Equal("#111827", renderer.LastSpec!.Primary);    // from FakeBrand
        Assert.Equal("#6b7280", renderer.LastSpec.Secondary);
        Assert.Equal("#2563eb", renderer.LastSpec.Accent);
    }

    [Fact]
    public async Task Create_persists_the_spec_and_get_returns_it_for_reopening()
    {
        using var db = TestDb.New(tenantId: 1);
        var created = await New(db).CreateAsync(
            new PosterStudioRequest("org", null, "Big Diwali Sale", 999m, "Shop Now", true, true, TemplateId: "minimal-type", Format: "story"), null);

        var detail = await New(db).GetAsync(created.CreativeId);

        Assert.Equal("poster", detail.Type);
        Assert.NotNull(detail.Poster);
        Assert.Equal("Big Diwali Sale", detail.Poster!.Headline);
        Assert.Equal("minimal-type", detail.Poster.TemplateId);
        Assert.Equal("story", detail.Poster.Format);
        Assert.Equal(created.Caption, detail.Caption);
    }

    [Fact]
    public async Task Get_returns_null_poster_for_a_text_creative()
    {
        using var db = TestDb.New(tenantId: 1);
        var item = new MarketingPlanItem { MarketingPlanId = 1, Type = "text", Topic = "T", Channels = "", Status = "approved", ScheduledAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        db.MarketingPlanItems.Add(item);
        await db.SaveChangesAsync();
        var creative = new MarketingCreative { MarketingPlanItemId = item.MarketingPlanItemId, Type = "text", Status = "generated", Body = "Some copy", CreatedAt = DateTime.UtcNow };
        db.MarketingCreatives.Add(creative);
        await db.SaveChangesAsync();

        var detail = await New(db).GetAsync(creative.MarketingCreativeId);

        Assert.Null(detail.Poster);
        Assert.Equal("Some copy", detail.Caption);
    }

    [Fact]
    public async Task Update_re_renders_in_place_without_spending_a_credit_and_keeps_the_same_ids()
    {
        using var db = TestDb.New(tenantId: 1);
        var renderer = new FakeRenderer();
        var copy = new FakeCopywriter();
        var created = await New(db, renderer, copy).CreateAsync(
            new PosterStudioRequest("org", null, "First", null, "Shop Now", true, true), null);
        Assert.Equal(1, copy.Calls);

        var updated = await New(db, renderer, copy).UpdateAsync(
            created.CreativeId, new PosterStudioRequest("org", null, "Updated Headline", 499m, "Buy Now", true, true), "Hand-edited caption", null);

        Assert.Equal(created.ItemId, updated.ItemId);
        Assert.Equal(created.CreativeId, updated.CreativeId);
        Assert.Equal("Hand-edited caption", updated.Caption);
        Assert.Equal(1, copy.Calls);                          // no new AI caption call — free edit
        Assert.Single(db.MarketingCreatives);                 // updated in place, not a new row
        Assert.Contains("Updated Headline", renderer.LastSpec!.Headline);
    }

    [Fact]
    public async Task Update_rejects_a_creative_that_is_not_a_poster()
    {
        using var db = TestDb.New(tenantId: 1);
        var item = new MarketingPlanItem { MarketingPlanId = 1, Type = "text", Topic = "T", Channels = "", Status = "approved", ScheduledAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        db.MarketingPlanItems.Add(item);
        await db.SaveChangesAsync();
        var creative = new MarketingCreative { MarketingPlanItemId = item.MarketingPlanItemId, Type = "text", Status = "generated", Body = "Copy", CreatedAt = DateTime.UtcNow };
        db.MarketingCreatives.Add(creative);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            New(db).UpdateAsync(creative.MarketingCreativeId, new PosterStudioRequest("org", null, "X", null, "Shop Now", true, true), "Caption", null));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Duplicate_clones_the_creative_into_a_new_independently_editable_item()
    {
        using var db = TestDb.New(tenantId: 1);
        var created = await New(db).CreateAsync(
            new PosterStudioRequest("org", null, "Original", 199m, "Shop Now", true, true), null);

        var dup = await New(db).DuplicateAsync(created.CreativeId);

        Assert.NotEqual(created.CreativeId, dup.CreativeId);
        Assert.NotEqual(created.ItemId, dup.ItemId);
        Assert.Equal(created.MediaUrl, dup.MediaUrl);         // copied as-is, no re-render
        Assert.Equal(created.Caption, dup.Caption);
        Assert.Equal(2, db.MarketingCreatives.Count());

        var dupDetail = await New(db).GetAsync(dup.CreativeId);
        Assert.NotNull(dupDetail.Poster);                     // the copy is independently editable
        Assert.Equal("Original", dupDetail.Poster!.Headline);
    }

    [Fact]
    public async Task Duplicate_rejects_a_creative_that_is_not_a_poster()
    {
        using var db = TestDb.New(tenantId: 1);
        var item = new MarketingPlanItem { MarketingPlanId = 1, Type = "text", Topic = "T", Channels = "", Status = "approved", ScheduledAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        db.MarketingPlanItems.Add(item);
        await db.SaveChangesAsync();
        var creative = new MarketingCreative { MarketingPlanItemId = item.MarketingPlanItemId, Type = "text", Status = "generated", Body = "Copy", CreatedAt = DateTime.UtcNow };
        db.MarketingCreatives.Add(creative);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<AppException>(() => New(db).DuplicateAsync(creative.MarketingCreativeId));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Auto_fill_picks_a_photo_capable_template_for_a_product_with_a_photo()
    {
        using var db = TestDb.New(tenantId: 1);
        var copy = new FakeCopywriter { Response = "Elegance for every celebration." };

        var draft = await New(db, copy: copy).AutoFillDraftAsync("product", 3, CancellationToken.None);   // product 3 has an image (FakeCatalog)

        Assert.Equal("bold-medallion", draft.TemplateId);   // the FakeRenderer template with UsesPhoto=true
        Assert.Equal("Elegance for every celebration", draft.Headline);
        Assert.True(draft.ShowPrice);                       // product 3 has a price
    }

    [Fact]
    public async Task Auto_fill_picks_a_typography_template_for_an_organization_poster()
    {
        using var db = TestDb.New(tenantId: 1);

        var draft = await New(db).AutoFillDraftAsync("org", null, CancellationToken.None);

        Assert.Equal("minimal-type", draft.TemplateId);     // the FakeRenderer template with UsesPhoto=false
        Assert.False(draft.ShowPrice);                      // no product to price
    }

    [Fact]
    public async Task Auto_fill_never_triggers_the_paid_background_generation()
    {
        using var db = TestDb.New(tenantId: 1);
        var growthImages = new FakeGrowthImages();
        var credits = new FakeCreditService();

        await New(db, growthImages: growthImages, credits: credits).AutoFillDraftAsync("product", 3, CancellationToken.None);

        Assert.Equal(0, growthImages.Calls);
        Assert.Equal(0, credits.Calls);
    }
}
