using System.Text;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Growth;
using ecomm.api.Features.Media;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>What the merchant controls when crafting one poster by hand — organization or product-led,
/// with full override of what's rendered on the image. <paramref name="BackgroundImageUrl"/> is set
/// once <see cref="IPosterStudioService.GenerateBackgroundAsync"/> has produced an AI scene; omit it to
/// keep the plain brand-gradient look.</summary>
public sealed record PosterStudioRequest(
    string Kind,                 // "org" | "product"
    long? ProductId,
    string Headline,
    decimal? Price,              // null = no price badge, even if the product has one
    string Cta,
    bool IncludeLogo,
    bool IncludeName,
    string? BackgroundImageUrl = null);

public sealed record PosterPreviewResult(string Svg);
public sealed record PosterCreatedResult(long ItemId, long CreativeId, string MediaUrl, string Caption);
public sealed record SuggestHeadlineResult(string Headline);
public sealed record PosterBackgroundStyleDto(string Key, string Label, string Description);
public sealed record PosterBackgroundResult(string Url, int CreditsSpent);

public interface IPosterStudioService
{
    /// <summary>Renders a poster from the given inputs and returns raw SVG markup — no persistence, no
    /// credit spend. For live-editing: call this on every "Preview"/"Regenerate" click.</summary>
    Task<PosterPreviewResult> PreviewAsync(PosterStudioRequest req, CancellationToken ct = default);

    IReadOnlyList<PosterBackgroundStyleDto> BackgroundStyles();

    /// <summary>Generates a real AI scene (via the same image pipeline "Product images" uses) to sit
    /// behind the poster's text, instead of the plain brand gradient — credit-metered (real provider
    /// cost), opt-in. For a product poster this rides the proven product-image generator directly (same
    /// quality the merchant already sees on Product images); for an organization poster it prompts from
    /// the brand kit. The renderer still draws the headline/price/CTA on top — never the model — since
    /// image models render exact text/prices unreliably.</summary>
    Task<PosterBackgroundResult> GenerateBackgroundAsync(PosterStudioRequest req, string style, long? userId, CancellationToken ct = default);

    /// <summary>AI-drafted headline options for the product/topic — one metered call, opt-in (never
    /// fired automatically while the merchant is just typing).</summary>
    Task<SuggestHeadlineResult> SuggestHeadlineAsync(long? productId, string? topic, CancellationToken ct = default);

    /// <summary>Finalizes the poster the merchant is happy with: stores the image, writes an AI caption
    /// (credit-metered) and creates a standalone MarketingCreative — ready to be scheduled to a channel
    /// via <see cref="IMarketingGenerationService.ScheduleExistingAsync"/>. Lives outside any specific
    /// week's plan (an internal "ad hoc" bucket) so it doesn't interfere with "This week".</summary>
    Task<PosterCreatedResult> CreateAsync(PosterStudioRequest req, long? userId, CancellationToken ct = default);
}

/// <summary>
/// A standalone poster editor (Marketing Studio, per the user's "make it more flexible, like a real
/// editor" ask) — separate from the weekly-plan batch flow. Two-phase by design to protect credits:
/// Preview is free (deterministic SVG render only) so the merchant can iterate on headline/price/CTA/
/// toggles as many times as they like; Create is the one metered step (an AI caption for the eventual
/// post) that persists the result. Reuses the same renderer/brand-kit/catalog the weekly plan uses.
/// </summary>
public sealed class PosterStudioService(
    EcommerceDbContext db, IPosterRenderer renderer, IMarketingBrandService brandService,
    ICatalogReader catalog, IMarketingCopywriter copywriter, IMediaStorage media,
    IGrowthImageService growthImages, IAiCreditService credits) : IPosterStudioService
{
    // Sentinel far outside any real computed week-start date — guarantees the ad-hoc bucket can never
    // collide with AutoDraftForCurrentTenantAsync's "does a plan already exist for this week" check.
    private static readonly DateTime AdHocWeekStart = new(1970, 1, 1);

    // Org-level (no product) prompts, keyed to the same style labels the product-image generator uses
    // (reused as-is for products via IGrowthImageService) so the picker reads consistently either way.
    private static readonly Dictionary<string, (string Label, string Description, string Instruction)> OrgStyles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lifestyle"] = ("Lifestyle photo", "A natural, in-use retail scene.",
            "A photorealistic lifestyle retail scene evoking the brand, warm natural light, shallow depth of field, tasteful negative space in the lower third for a text overlay. No text, no watermark, no logos."),
        ["studio"] = ("Studio shot", "Clean, minimal studio backdrop.",
            "A clean, minimal studio backdrop in soft even light, subtle brand-appropriate colour tones, elegant negative space in the lower third for a text overlay. No text, no watermark, no logos."),
        ["festive"] = ("Festive poster", "A celebratory festival-themed scene.",
            "A vibrant festive promotional scene for an Indian festival, warm celebratory colours and decorative elements, clear open space in the lower third for a text overlay. No text, no watermark, no logos."),
        ["flatlay"] = ("Flat lay", "An overhead styled composition.",
            "An overhead flat-lay photograph with tasteful props on a textured surface, balanced composition, bright even light, open space in the lower third for a text overlay. No text, no watermark, no logos."),
    };

    public IReadOnlyList<PosterBackgroundStyleDto> BackgroundStyles() =>
        OrgStyles.Select(kv => new PosterBackgroundStyleDto(kv.Key, kv.Value.Label, kv.Value.Description)).ToList();

    public async Task<PosterPreviewResult> PreviewAsync(PosterStudioRequest req, CancellationToken ct = default)
    {
        var spec = await BuildSpecAsync(req, ct);
        return new PosterPreviewResult(await renderer.RenderSvgAsync(spec, ct));
    }

    public async Task<PosterBackgroundResult> GenerateBackgroundAsync(PosterStudioRequest req, string style, long? userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(style) || !OrgStyles.ContainsKey(style))
            throw new AppException("Unknown background style.", StatusCodes.Status400BadRequest);

        if (req.Kind == "product")
        {
            if (req.ProductId is not { } pid)
                throw new AppException("Choose a product for a product poster.", StatusCodes.Status400BadRequest);
            // Ride the same, already-proven product-image pipeline the merchant sees on Product images —
            // same quality, same 20-credit price, no duplicate prompt engineering.
            var image = await growthImages.GenerateAsync(new GenerateImageRequest(pid, style, "instagram-post", req.Headline), userId, ct);
            return new PosterBackgroundResult(image.Url, AiCreditPricing.CostOf(AiCreditPricing.GrowthImage));
        }

        // Organization poster — no product to anchor a prompt on, so build one from the brand kit.
        var brand = await brandService.GetAsync(ct);
        var (_, _, instruction) = OrgStyles[style];
        var subject = string.IsNullOrWhiteSpace(brand.CompanyName) ? "a retail store" : $"the brand \"{brand.CompanyName}\"";
        var prompt = $"A marketing scene for {subject}. {instruction}";

        var url = await credits.MeterImageAsync(AiCreditPricing.GrowthImage, async img =>
        {
            var image = await img.GenerateAsync(new ImagePrompt(prompt, "1024x1024"), ct);
            using var stream = new MemoryStream(image.Bytes);
            var stored = await media.SaveAsync(stream, $"poster-bg-{style}.png", image.ContentType, ct);
            return (stored.Url, image);
        }, ct);

        return new PosterBackgroundResult(url, AiCreditPricing.CostOf(AiCreditPricing.GrowthImage));
    }

    public async Task<SuggestHeadlineResult> SuggestHeadlineAsync(long? productId, string? topic, CancellationToken ct = default)
    {
        var product = productId is { } pid ? await catalog.GetAsync(pid, ct) : null;
        var subject = product?.Name ?? topic ?? "our store";
        var brief = $"Write ONE short, punchy poster headline (max 8 words, no quotes, no hashtags) for {subject}.";
        var text = await copywriter.WriteAsync("instagram-caption", productId, brief, null, ct);
        // Copy generation returns a fuller caption — take the first line/sentence as the headline.
        var headline = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? subject;
        headline = headline.Split('.').First().Trim(' ', '"', '“', '”');
        if (headline.Length > 60) headline = headline[..60];
        return new SuggestHeadlineResult(string.IsNullOrWhiteSpace(headline) ? subject : headline);
    }

    public async Task<PosterCreatedResult> CreateAsync(PosterStudioRequest req, long? userId, CancellationToken ct = default)
    {
        var spec = await BuildSpecAsync(req, ct);
        var svg = await renderer.RenderSvgAsync(spec, ct);

        var planId = await GetOrCreateAdHocPlanAsync(ct);
        var now = DateTime.UtcNow;
        var item = new MarketingPlanItem
        {
            MarketingPlanId = planId,
            Type = "poster",
            Topic = Clean(req.Headline, 300) ?? "Poster",
            ProductId = req.Kind == "product" ? req.ProductId : null,
            Channels = "",
            IncludeLogo = req.IncludeLogo,
            IncludeName = req.IncludeName,
            Status = "proposed",
            ScheduledAt = now,
            CreatedAt = now,
        };
        db.MarketingPlanItems.Add(item);
        await db.SaveChangesAsync(ct);

        var bytes = Encoding.UTF8.GetBytes(svg);
        var stored = await media.SaveAsync(new MemoryStream(bytes), $"poster-{item.MarketingPlanItemId}.svg", "image/svg+xml", ct);

        var kind = req.Kind == "product" ? "product-description" : "instagram-caption";
        var caption = await copywriter.WriteAsync(kind, item.ProductId, req.Headline, userId, ct);

        var creative = new MarketingCreative
        {
            MarketingPlanItemId = item.MarketingPlanItemId,
            Type = "poster",
            Status = "generated",
            Body = caption,
            OutputMediaUrl = stored.Url,
            ProductId = item.ProductId,
            CreatedAt = now,
        };
        db.MarketingCreatives.Add(creative);
        item.Status = "approved";
        item.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return new PosterCreatedResult(item.MarketingPlanItemId, creative.MarketingCreativeId, stored.Url, caption);
    }

    private async Task<PosterSpec> BuildSpecAsync(PosterStudioRequest req, CancellationToken ct)
    {
        if (req.Kind == "product" && req.ProductId is null)
            throw new AppException("Choose a product for a product poster.", StatusCodes.Status400BadRequest);

        var brand = await brandService.GetAsync(ct);
        CatalogProduct? product = req.Kind == "product" && req.ProductId is { } pid ? await catalog.GetAsync(pid, ct) : null;
        var headline = Clean(req.Headline, 120) ?? product?.Name ?? brand.CompanyName ?? "Your store";
        var cta = Clean(req.Cta, 40) ?? "Shop Now";

        return new PosterSpec(
            req.Kind == "product" ? "product" : "org", headline, req.Price, cta,
            brand.CompanyName, req.IncludeName, req.IncludeLogo, brand.LogoUrl, product?.ImageUrl,
            brand.PrimaryColor, brand.SecondaryColor, brand.AccentColor, brand.Font, req.BackgroundImageUrl);
    }

    private async Task<long> GetOrCreateAdHocPlanAsync(CancellationToken ct)
    {
        var existing = await db.MarketingPlans.FirstOrDefaultAsync(p => p.Status == "adhoc", ct);
        if (existing is not null) return existing.MarketingPlanId;

        var plan = new MarketingPlan { WeekStart = AdHocWeekStart, Status = "adhoc", CreatedAt = DateTime.UtcNow };
        db.MarketingPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return plan.MarketingPlanId;
    }

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
