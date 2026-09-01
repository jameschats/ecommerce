using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
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
public sealed class MarketingGenerationService(EcommerceDbContext db, IMarketingCopywriter copywriter)
    : IMarketingGenerationService
{
    public async Task<GenerationResult> GenerateForPlanAsync(long planId, long? userId, CancellationToken ct = default)
    {
        var items = await db.MarketingPlanItems
            .Where(i => i.MarketingPlanId == planId && i.Status == "proposed")
            .OrderBy(i => i.SortOrder)
            .ToListAsync(ct);

        int creatives = 0, posts = 0, skipped = 0, failed = 0;
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            var channels = Split(item.Channels);

            // Only text is generated in 3a; posters/videos wait for their renderer.
            if (item.Type != "text") { skipped++; continue; }
            if (channels.Count == 0) { skipped++; continue; }   // nowhere to post → don't spend credits

            try
            {
                var kind = KindFor(channels[0]);
                var brief = string.IsNullOrWhiteSpace(item.Angle) ? item.Topic : $"{item.Topic} — {item.Angle}";
                var body = await copywriter.WriteAsync(kind, item.ProductId, brief, userId, ct);

                var creative = new MarketingCreative
                {
                    MarketingPlanItemId = item.MarketingPlanItemId,
                    Type = "text",
                    Status = "generated",
                    Body = body,
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
