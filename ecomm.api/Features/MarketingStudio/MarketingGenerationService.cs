using System.Text;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Media;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>Outcome of generating a confirmed plan: how many creatives were made and posts scheduled.</summary>
public sealed record GenerationResult(int CreativesGenerated, int PostsScheduled, int Skipped, int Failed);

public interface IMarketingGenerationService
{
    /// <summary>Generate creatives for every not-yet-generated item in a plan and fan out one
    /// scheduled post per channel. Idempotent: items already generated are left alone. Credit-metered
    /// per copy via the copywriter (debits on success only).</summary>
    Task<GenerationResult> GenerateForPlanAsync(long planId, long? userId, CancellationToken ct = default);
}

/// <summary>
/// Turns a confirmed weekly plan into real creatives + scheduled posts (MS2 sub-step 3a — text). For
/// each text item with at least one target channel it writes one caption (reused across the item's
/// channels), stores a MarketingCreative, and creates a ScheduledPost per channel at
/// <c>pending_approval</c> (the D5 approval gate; auto-publish opt-in + the publish sweep are
/// sub-step 4). Poster/video items are left for sub-step 3b/MS3. Robust per item: one failure is
/// recorded and the batch continues.
/// </summary>
public sealed class MarketingGenerationService(
    EcommerceDbContext db, IMarketingCopywriter copywriter, IMarketingBrandService brandService,
    ICatalogReader catalog, IPosterRenderer poster, IMediaStorage media) : IMarketingGenerationService
{
    public async Task<GenerationResult> GenerateForPlanAsync(long planId, long? userId, CancellationToken ct = default)
    {
        var items = await db.MarketingPlanItems
            .Where(i => i.MarketingPlanId == planId && i.Status == "proposed")
            .OrderBy(i => i.SortOrder)
            .ToListAsync(ct);

        int creatives = 0, posts = 0, skipped = 0, failed = 0;
        var now = DateTime.UtcNow;
        MarketingBrandDto? brand = null;   // loaded once, lazily

        foreach (var item in items)
        {
            var channels = Split(item.Channels);
            if (item.Type is not ("text" or "poster")) { skipped++; continue; }   // video → MS3
            if (channels.Count == 0) { skipped++; continue; }                      // nowhere to post → don't spend credits

            try
            {
                // Every post carries a caption (credit-metered copy).
                var kind = KindFor(channels[0]);
                var brief = string.IsNullOrWhiteSpace(item.Angle) ? item.Topic : $"{item.Topic} — {item.Angle}";
                var caption = await copywriter.WriteAsync(kind, item.ProductId, brief, userId, ct);

                string? mediaUrl = null;
                if (item.Type == "poster")
                {
                    brand ??= await brandService.GetAsync(ct);
                    var svg = await poster.RenderSvgAsync(await BuildPosterSpecAsync(item, brand, ct), ct);
                    var bytes = Encoding.UTF8.GetBytes(svg);
                    var stored = await media.SaveAsync(new MemoryStream(bytes), $"poster-{item.MarketingPlanItemId}.svg", "image/svg+xml", ct);
                    mediaUrl = stored.Url;
                }

                var creative = new MarketingCreative
                {
                    MarketingPlanItemId = item.MarketingPlanItemId,
                    Type = item.Type,
                    Status = "generated",
                    Body = caption,
                    OutputMediaUrl = mediaUrl,
                    ProductId = item.ProductId,
                    CreatedAt = now,
                };
                db.MarketingCreatives.Add(creative);
                await db.SaveChangesAsync(ct);   // get the creative id for the posts
                creatives++;

                foreach (var platform in channels)
                {
                    db.ScheduledPosts.Add(new ScheduledPost
                    {
                        MarketingPlanItemId = item.MarketingPlanItemId,
                        MarketingCreativeId = creative.MarketingCreativeId,
                        Platform = platform,
                        ScheduledAt = item.ScheduledAt,
                        Status = "pending_approval",
                        CreatedAt = now,
                    });
                    posts++;
                }

                item.Status = "approved";
                item.UpdatedAt = now;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception)
            {
                failed++;
                // leave the item at "proposed" so a later retry can pick it up
            }
        }

        return new GenerationResult(creatives, posts, skipped, failed);
    }

    private async Task<PosterSpec> BuildPosterSpecAsync(MarketingPlanItem item, MarketingBrandDto brand, CancellationToken ct)
    {
        CatalogProduct? product = item.ProductId is { } pid ? await catalog.GetAsync(pid, ct) : null;
        var kind = product is not null ? "product" : "org";
        var headline = product?.Name ?? item.Topic;
        return new PosterSpec(
            kind, headline, product?.Price, "Shop Now",
            brand.CompanyName, item.IncludeName, item.IncludeLogo, brand.LogoUrl, product?.ImageUrl,
            brand.PrimaryColor, brand.SecondaryColor, brand.AccentColor, brand.Font);
    }

    /// <summary>Map a platform to the closest Growth content-type key for copy generation.</summary>
    private static string KindFor(string platform) => platform.ToLowerInvariant() switch
    {
        "instagram" or "pinterest" => "instagram-caption",
        "facebook" or "linkedin" or "youtube" => "facebook-post",
        "whatsapp" => "whatsapp",
        "googleads" => "google-ads",
        _ => "instagram-caption",
    };

    private static List<string> Split(string csv) =>
        string.IsNullOrWhiteSpace(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
