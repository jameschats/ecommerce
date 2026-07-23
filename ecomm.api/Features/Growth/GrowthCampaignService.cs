using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Growth;

public sealed record GoalDto(string Key, string Label, string Description);
public sealed record CreateCampaignRequest(string Goal, string? Name, long ProductId, string? Brief, string? Language);
public sealed record CampaignChannelDto(string Channel, GrowthContentDto? Content, string? Error);
public sealed record CampaignDto(
    long Id, string Name, string Goal, long? ProductId, string Language, string Status,
    DateTime CreatedAt, IReadOnlyList<CampaignChannelDto> Channels);
public sealed record CampaignSummaryDto(long Id, string Name, string Goal, string Status, DateTime CreatedAt, int Pieces);

public interface IGrowthCampaignService
{
    IReadOnlyList<GoalDto> Goals();
    Task<CampaignDto> CreateAsync(CreateCampaignRequest req, long? userId, CancellationToken ct = default);
    Task<PagedResult<CampaignSummaryDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<CampaignDto> GetAsync(long id, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Campaign builder (G2): one goal fans out to four channels in a single action. Orchestrates
/// <see cref="IGrowthGenerationService"/> — each channel is a separate metered AI call, so a
/// four-channel campaign debits the four channel costs and produces four editable
/// <see cref="GrowthContent"/> rows linked to the campaign.
///
/// Fan-out is fault-tolerant: if one channel fails (out of credits, provider hiccup) the campaign
/// keeps the channels that succeeded and reports the failure per channel, rather than losing the lot.
/// </summary>
public sealed class GrowthCampaignService(EcommerceDbContext db, IGrowthGenerationService gen) : IGrowthCampaignService
{
    /// <summary>The channels a campaign fans out to. All four are product-based owned/prepared channels.</summary>
    private static readonly string[] Channels = { "instagram-caption", "facebook-post", "whatsapp", "email" };

    private sealed record GoalDef(string Key, string Label, string Description, string Brief);

    private static readonly IReadOnlyList<GoalDef> GoalDefs = new List<GoalDef>
    {
        new("new-arrival", "New arrival", "Announce a product just added to the store.",
            "Announce this product as an exciting new arrival that just landed in the store."),
        new("festival", "Festival offer", "Tie a promotion to a festival or occasion.",
            "Promote this product as a festive special, tied to the occasion in the merchant's note."),
        new("weekend-sale", "Weekend sale", "A short, time-boxed weekend push.",
            "Promote a limited weekend sale on this product; create gentle urgency without inventing a discount unless the merchant gave one."),
        new("restock", "Back in stock", "Tell customers a popular item has returned.",
            "Announce that this popular product is back in stock, and encourage customers to grab it before it sells out again."),
        new("clearance", "Clearance", "Move end-of-line stock.",
            "Promote this product as a clearance offer; encourage a quick purchase, but do not invent a specific discount unless the merchant gave one."),
    };

    private static readonly Dictionary<string, GoalDef> GoalByKey =
        GoalDefs.ToDictionary(g => g.Key, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<GoalDto> Goals() =>
        GoalDefs.Select(g => new GoalDto(g.Key, g.Label, g.Description)).ToList();

    public async Task<CampaignDto> CreateAsync(CreateCampaignRequest req, long? userId, CancellationToken ct = default)
    {
        if (!GoalByKey.TryGetValue(req.Goal ?? "", out var goal))
            throw new AppException("Unknown campaign goal.", StatusCodes.Status400BadRequest);

        var product = await db.Products.AsNoTracking()
            .Where(p => p.ProductId == req.ProductId)
            .Select(p => new { p.Name })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException("Pick a product for this campaign.", StatusCodes.Status404NotFound);

        var name = Clean(req.Name, 200) ?? $"{goal.Label} — {product.Name}";
        var brief = string.IsNullOrWhiteSpace(req.Brief) ? goal.Brief : $"{goal.Brief} {req.Brief!.Trim()}";

        var campaign = new GrowthCampaign
        {
            Name = name, Goal = goal.Key, ProductId = req.ProductId,
            Language = string.IsNullOrWhiteSpace(req.Language) ? "English" : req.Language!.Trim(),
            Status = "Draft", CreatedByUserId = userId, CreatedAt = DateTime.UtcNow,
        };
        db.GrowthCampaigns.Add(campaign);
        await db.SaveChangesAsync(ct);

        // One metered AI call per channel. Fault-tolerant: a failure on one channel (e.g. credits run
        // out partway) is reported for that channel and the rest are kept.
        var results = new List<CampaignChannelDto>();
        foreach (var channel in Channels)
        {
            try
            {
                var content = await gen.GenerateAsync(
                    new GenerateRequest(channel, req.ProductId, req.Language, brief), userId, campaign.GrowthCampaignId, ct);
                results.Add(new CampaignChannelDto(channel, content, null));
            }
            catch (AppException ex)
            {
                results.Add(new CampaignChannelDto(channel, null, ex.Message));
            }
        }

        return new CampaignDto(
            campaign.GrowthCampaignId, campaign.Name, campaign.Goal, campaign.ProductId,
            campaign.Language, campaign.Status, campaign.CreatedAt, results);
    }

    public async Task<PagedResult<CampaignSummaryDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = db.GrowthCampaigns.AsNoTracking();
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(c => c.GrowthCampaignId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new CampaignSummaryDto(
                c.GrowthCampaignId, c.Name, c.Goal, c.Status, c.CreatedAt,
                db.GrowthContents.Count(gc => gc.CampaignId == c.GrowthCampaignId)))
            .ToListAsync(ct);

        return new PagedResult<CampaignSummaryDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<CampaignDto> GetAsync(long id, CancellationToken ct = default)
    {
        var campaign = await db.GrowthCampaigns.AsNoTracking().FirstOrDefaultAsync(c => c.GrowthCampaignId == id, ct)
                       ?? throw new AppException("Campaign not found.", StatusCodes.Status404NotFound);

        var content = await db.GrowthContents.AsNoTracking()
            .Where(gc => gc.CampaignId == id)
            .OrderBy(gc => gc.GrowthContentId)
            .Select(gc => new GrowthContentDto(gc.GrowthContentId, gc.ContentType, gc.ProductId, gc.Language, gc.Title, gc.Body, gc.Status, gc.CreatedAt))
            .ToListAsync(ct);

        var channels = content.Select(c => new CampaignChannelDto(c.ContentType, c, null)).ToList();
        return new CampaignDto(campaign.GrowthCampaignId, campaign.Name, campaign.Goal, campaign.ProductId,
            campaign.Language, campaign.Status, campaign.CreatedAt, channels);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var campaign = await db.GrowthCampaigns.FirstOrDefaultAsync(c => c.GrowthCampaignId == id, ct)
                       ?? throw new AppException("Campaign not found.", StatusCodes.Status404NotFound);

        var pieces = await db.GrowthContents.Where(gc => gc.CampaignId == id).ToListAsync(ct);
        db.GrowthContents.RemoveRange(pieces);
        db.GrowthCampaigns.Remove(campaign);
        await db.SaveChangesAsync(ct);
    }

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
