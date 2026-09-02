using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>One saved creative — a poster or text post, wherever it came from (a weekly plan or the
/// standalone Poster Studio) — with where (if anywhere) it's currently scheduled. <paramref name="Editable"/>
/// is true only for posters that persisted their spec (see MarketingCreative.Spec) — older posters and
/// text creatives can still be viewed/scheduled/duplicated here, just not reopened in the Editor.</summary>
public sealed record LibraryItemDto(
    long CreativeId, long ItemId, string Type, string? Body, string? MediaUrl,
    long? ProductId, DateTime CreatedAt, IReadOnlyList<string> Channels, bool Editable);

public interface IMarketingLibraryService
{
    /// <summary>Every generated creative for the tenant, newest first — the single place a merchant can
    /// browse everything the Studio has ever made (from "This week" or Poster Studio alike) and, for
    /// anything not yet scheduled, assign it a channel via <see cref="IMarketingGenerationService.ScheduleExistingAsync"/>.</summary>
    Task<IReadOnlyList<LibraryItemDto>> ListAsync(string? type, CancellationToken ct = default);
}

/// <summary>
/// The Creative Library — answers "where did my poster go?" by giving every MarketingCreative a
/// permanent, browsable home regardless of which flow made it (weekly-plan generation or the ad-hoc
/// Poster Studio bucket). Read-only; scheduling/assignment reuses the existing plan-item endpoints.
/// </summary>
public sealed class MarketingLibraryService(EcommerceDbContext db) : IMarketingLibraryService
{
    public async Task<IReadOnlyList<LibraryItemDto>> ListAsync(string? type, CancellationToken ct = default)
    {
        var q = db.MarketingCreatives.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(c => c.Type == type);

        var creatives = await q.OrderByDescending(c => c.MarketingCreativeId).Take(200).ToListAsync(ct);
        if (creatives.Count == 0) return [];

        var itemIds = creatives.Select(c => c.MarketingPlanItemId).Distinct().ToList();
        var channelsByItem = await db.ScheduledPosts.AsNoTracking()
            .Where(p => itemIds.Contains(p.MarketingPlanItemId))
            .GroupBy(p => p.MarketingPlanItemId)
            .Select(g => new { ItemId = g.Key, Platforms = g.Select(p => p.Platform).ToList() })
            .ToDictionaryAsync(x => x.ItemId, x => x.Platforms, ct);

        return creatives.Select(c => new LibraryItemDto(
            c.MarketingCreativeId, c.MarketingPlanItemId, c.Type, c.Body, c.OutputMediaUrl,
            c.ProductId, c.CreatedAt, channelsByItem.GetValueOrDefault(c.MarketingPlanItemId, []),
            Editable: c.Type == "poster" && !string.IsNullOrWhiteSpace(c.Spec))).ToList();
    }
}
