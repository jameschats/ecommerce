using System.Text;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Media;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>What the merchant controls when crafting one poster by hand — organization or product-led,
/// with full override of what's rendered on the image.</summary>
public sealed record PosterStudioRequest(
    string Kind,                 // "org" | "product"
    long? ProductId,
    string Headline,
    decimal? Price,              // null = no price badge, even if the product has one
    string Cta,
    bool IncludeLogo,
    bool IncludeName);

public sealed record PosterPreviewResult(string Svg);
public sealed record PosterCreatedResult(long ItemId, long CreativeId, string MediaUrl, string Caption);
public sealed record SuggestHeadlineResult(string Headline);

public interface IPosterStudioService
{
    /// <summary>Renders a poster from the given inputs and returns raw SVG markup — no persistence, no
    /// credit spend. For live-editing: call this on every "Preview"/"Regenerate" click.</summary>
    Task<PosterPreviewResult> PreviewAsync(PosterStudioRequest req, CancellationToken ct = default);

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
    ICatalogReader catalog, IMarketingCopywriter copywriter, IMediaStorage media) : IPosterStudioService
{
    // Sentinel far outside any real computed week-start date — guarantees the ad-hoc bucket can never
    // collide with AutoDraftForCurrentTenantAsync's "does a plan already exist for this week" check.
    private static readonly DateTime AdHocWeekStart = new(1970, 1, 1);

    public async Task<PosterPreviewResult> PreviewAsync(PosterStudioRequest req, CancellationToken ct = default)
    {
        var spec = await BuildSpecAsync(req, ct);
        return new PosterPreviewResult(await renderer.RenderSvgAsync(spec, ct));
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
            brand.PrimaryColor, brand.SecondaryColor, brand.AccentColor, brand.Font);
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
