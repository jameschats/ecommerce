using System.Text;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Media;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>Outcome of generating a confirmed plan. <c>Unscheduled</c> (a subset of
/// <c>CreativesGenerated</c>) is content that WAS generated but has no channel to post to yet — the
/// merchant can see it immediately and assign a channel later via <see cref="IMarketingGenerationService.ScheduleExistingAsync"/>,
/// with no extra credit spend.</summary>
public sealed record GenerationResult(int CreativesGenerated, int PostsScheduled, int Unscheduled, int Skipped, int Failed);

public sealed record ScheduleExistingResult(int PostsScheduled);

public interface IMarketingGenerationService
{
    /// <summary>Generate creatives for every not-yet-generated item in a plan and fan out one
    /// scheduled post per channel the item already has. Content is generated even for items with no
    /// channel selected yet — so the merchant can see what the AI made before connecting anything —
    /// it's just left unscheduled. Idempotent: items already generated are left alone. Credit-metered
    /// per copy via the copywriter (debits on success only, regardless of channel).</summary>
    Task<GenerationResult> GenerateForPlanAsync(long planId, long? userId, CancellationToken ct = default);

    /// <summary>Assign channels to an already-generated item and fan out ScheduledPosts for them —
    /// reuses the existing MarketingCreative, no re-generation, no extra credit spend. For channels the
    /// item already has a ScheduledPost for, this is a no-op (idempotent).</summary>
    Task<ScheduleExistingResult> ScheduleExistingAsync(long itemId, IReadOnlyList<string> channels, CancellationToken ct = default);
}

/// <summary>
/// Turns a confirmed weekly plan into real creatives + scheduled posts (MS2 sub-step 3a/3b). For each
/// text/poster item it writes one caption (reused across the item's channels) and, for posters, renders
/// the brand-themed image — stores a MarketingCreative, and fans out a ScheduledPost per channel at
/// <c>pending_approval</c> (the D5 approval gate; auto-publish opt-in + the publish sweep are sub-step
/// 4). Generation never waits on a channel being connected — that would block the merchant from ever
/// seeing what the studio makes before they've connected anything. Video items are left for MS3. Robust
/// per item: one failure is recorded and the batch continues.
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

        int creatives = 0, posts = 0, unscheduled = 0, skipped = 0, failed = 0;
        var now = DateTime.UtcNow;
        MarketingBrandDto? brand = null;   // loaded once, lazily

        foreach (var item in items)
        {
            var channels = Split(item.Channels);
            if (item.Type is not ("text" or "poster")) { skipped++; continue; }   // video → MS3

            try
            {
                // Every post carries a caption (credit-metered copy) — generated regardless of whether
                // a channel is connected yet, so the merchant can see it either way.
                var kind = channels.Count > 0 ? KindFor(channels[0]) : "instagram-caption";
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
                if (channels.Count == 0) unscheduled++;

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

        return new GenerationResult(creatives, posts, unscheduled, skipped, failed);
    }

    public async Task<ScheduleExistingResult> ScheduleExistingAsync(long itemId, IReadOnlyList<string> channels, CancellationToken ct = default)
    {
        var requested = channels.Where(SocialPlatforms.IsKnown).Select(c => SocialPlatforms.Get(c)!.Key).Distinct().ToList();
        if (requested.Count == 0) throw new AppException("Choose at least one channel.", StatusCodes.Status400BadRequest);

        var item = await db.MarketingPlanItems.FirstOrDefaultAsync(i => i.MarketingPlanItemId == itemId, ct)
            ?? throw new AppException("Item not found.", StatusCodes.Status404NotFound);
        var creative = await db.MarketingCreatives.Where(c => c.MarketingPlanItemId == itemId)
            .OrderByDescending(c => c.MarketingCreativeId).FirstOrDefaultAsync(ct)
            ?? throw new AppException("This post hasn't been generated yet.", StatusCodes.Status409Conflict);

        var existingPlatforms = await db.ScheduledPosts.Where(p => p.MarketingPlanItemId == itemId)
            .Select(p => p.Platform).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var added = 0;
        foreach (var platform in requested.Except(existingPlatforms, StringComparer.OrdinalIgnoreCase))
        {
            db.ScheduledPosts.Add(new ScheduledPost
            {
                MarketingPlanItemId = itemId,
                MarketingCreativeId = creative.MarketingCreativeId,
                Platform = platform,
                ScheduledAt = item.ScheduledAt,
                Status = "pending_approval",
                CreatedAt = now,
            });
            added++;
        }

        // Remember the assignment on the item itself so it reflects reality going forward.
        var merged = existingPlatforms.Concat(requested).Distinct(StringComparer.OrdinalIgnoreCase);
        item.Channels = string.Join(",", merged);
        item.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return new ScheduleExistingResult(added);
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
