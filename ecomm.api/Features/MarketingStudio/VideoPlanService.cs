namespace ecomm.api.Features.MarketingStudio;

/// <summary>One scene in a reel: how long, what to show, and the on-screen caption.</summary>
public sealed record VideoScene(int DurationSeconds, string Visual, string Text);

/// <summary>A ready-to-voice, ready-to-render reel plan. The narration is the spoken track (feeds
/// TTS); the scenes carry the visuals + burned-in captions the render worker assembles.</summary>
public sealed record VideoPlan(
    string Hook, int DurationSeconds, string Aspect, string MusicVertical, string Narration,
    IReadOnlyList<VideoScene> Scenes);

public sealed record NamedProduct(long Id, string Name);
public sealed record VideoOptionsDto(IReadOnlyList<NamedCode> Goals, IReadOnlyList<NamedCode> Platforms, IReadOnlyList<NamedProduct> Products);
public sealed record VideoPlanRequest(long? ProductId, string Goal, string Platform);

public interface IVideoPlanService
{
    Task<VideoOptionsDto> OptionsAsync(CancellationToken ct = default);
    Task<VideoPlan> BuildAsync(VideoPlanRequest req, long? userId, CancellationToken ct = default);
}

/// <summary>
/// Builds a reel plan from Product → Goal → Platform (MS3·b). The narration (the intelligence) is
/// AI-written via the existing credit-metered copywriter; the scene structure + on-screen captions (the
/// assembly spec) are deterministic — faithful to the user's fixed hook→product→detail→price→CTA
/// template and far more reliable than asking an LLM for raw video JSON. Reads the catalog + brand only
/// through their ports (extraction seam). Rendering to MP4 is the next slice (render worker).
/// </summary>
public sealed class VideoPlanService(IMarketingCopywriter copywriter, ICatalogReader catalog, IMarketingBrandService brandService)
    : IVideoPlanService
{
    private static readonly IReadOnlyList<NamedCode> Goals = new[]
    {
        new NamedCode("product-promo", "Promote a product"), new NamedCode("new-arrival", "New arrival"),
        new NamedCode("festival", "Festival / occasion"), new NamedCode("sale", "Sale / offer"),
        new NamedCode("brand-story", "Brand story"),
    };
    private static readonly IReadOnlyList<NamedCode> Platforms = new[]
    {
        new NamedCode("instagram", "Instagram Reel"), new NamedCode("youtube", "YouTube Short"),
        new NamedCode("facebook", "Facebook"), new NamedCode("linkedin", "LinkedIn"), new NamedCode("pinterest", "Pinterest"),
    };

    public async Task<VideoOptionsDto> OptionsAsync(CancellationToken ct = default)
    {
        var products = (await catalog.TopProductsAsync(30, ct)).Select(p => new NamedProduct(p.ProductId, p.Name)).ToList();
        return new VideoOptionsDto(Goals, Platforms, products);
    }

    public async Task<VideoPlan> BuildAsync(VideoPlanRequest req, long? userId, CancellationToken ct = default)
    {
        var product = req.ProductId is { } pid ? await catalog.GetAsync(pid, ct) : null;
        var brand = await brandService.GetAsync(ct);
        var company = string.IsNullOrWhiteSpace(brand.CompanyName) ? "our store" : brand.CompanyName!;
        var goal = Goals.Any(g => g.Code == req.Goal) ? req.Goal : "product-promo";
        var subject = product?.Name ?? company;

        // AI writes the narration (one metered call); on-screen captions stay deterministic.
        var kind = req.Platform == "youtube" ? "facebook-post" : "instagram-caption";
        var brief = $"A short, punchy {DurationHint()}-second spoken video script (2-3 sentences) for a {GoalLabel(goal)} reel about {subject}"
                  + (product is { Price: > 0 } ? $", priced at Rs {product.Price:0}" : "") + ". Warm, energetic, ends with a call to action.";
        string narration;
        try { narration = await copywriter.WriteAsync(kind, product?.ProductId, brief, userId, ct); }
        catch { narration = $"{HookFor(goal)} {subject} — {(product is { Price: > 0 } ? $"now just Rs {product.Price:0}. " : "")}Shop now at {company}."; }

        var hook = HookFor(goal);
        var priceText = product is { Price: > 0 } ? $"₹{product.Price:0}" : "Great value";
        var scenes = new List<VideoScene>
        {
            new(4, "hero_product", hook),
            new(6, "zoom_product", subject),
            new(5, "detail_product", DetailFor(goal)),
            new(5, "multiple_product_images", priceText),
            new(4, "brand_logo", "Shop Now"),
        };

        return new VideoPlan(hook, scenes.Sum(s => s.DurationSeconds), AspectFor(req.Platform), MusicFor(goal), narration.Trim(), scenes);
    }

    private static int DurationHint() => 24;
    private static string GoalLabel(string g) => Goals.FirstOrDefault(x => x.Code == g)?.Name ?? "promotional";
    private static string HookFor(string goal) => goal switch
    {
        "festival" => "Celebrate the season",
        "sale" => "Don't miss out",
        "new-arrival" => "Just dropped",
        "brand-story" => "Made with care",
        _ => "You'll love this",
    };
    private static string DetailFor(string goal) => goal switch
    {
        "festival" => "Perfect for the occasion",
        "sale" => "Limited-time offer",
        "new-arrival" => "Fresh in store",
        "brand-story" => "Crafted to last",
        _ => "Quality you can feel",
    };
    private static string AspectFor(string platform) => platform switch
    {
        "instagram" or "youtube" => "9:16",
        "pinterest" => "2:3",
        _ => "1:1",
    };
    private static string MusicFor(string goal) => goal switch
    {
        "festival" => "festive", "sale" => "energetic", "brand-story" => "cinematic", _ => "upbeat",
    };
}
