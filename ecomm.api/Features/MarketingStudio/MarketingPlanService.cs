using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.MarketingStudio;

public sealed record PlanItemDto(
    long Id, DateTime ScheduledAt, string Type, long? ProductId, string Topic, string? Angle,
    IReadOnlyList<string> Channels, bool IncludeLogo, bool IncludeName, string Status);

public sealed record PlanDto(long Id, DateTime WeekStart, string Status, IReadOnlyList<PlanItemDto> Items);

public sealed record ProposePlanRequest(DateTime? WeekStart);
public sealed record UpdatePlanItemRequest(
    DateTime? ScheduledAt, string? Topic, string? Angle, IReadOnlyList<string>? Channels, bool? IncludeLogo, bool? IncludeName);
public sealed record AddPlanItemRequest(string Type, long? ProductId, string Topic, DateTime ScheduledAt, IReadOnlyList<string>? Channels);

public interface IMarketingPlanService
{
    Task<PlanDto> ProposeAsync(ProposePlanRequest req, CancellationToken ct = default);
    Task<PlanDto?> GetCurrentAsync(CancellationToken ct = default);
    Task<PlanItemDto> UpdateItemAsync(long itemId, UpdatePlanItemRequest req, CancellationToken ct = default);
    Task<PlanItemDto> AddItemAsync(AddPlanItemRequest req, CancellationToken ct = default);
    Task RemoveItemAsync(long itemId, CancellationToken ct = default);
    Task<PlanDto> ConfirmAsync(long planId, CancellationToken ct = default);
    Task DiscardAsync(long planId, CancellationToken ct = default);
}

/// <summary>
/// The weekly-plan proposer + review (MS2 sub-step 2). Proposes a cheap OUTLINE — day/type/channel/
/// subject drawn deterministically from the tenant's preferences, the festival calendar and the
/// catalog — with NO creative generation and NO credits spent. The merchant edits/removes/adds/toggles,
/// then confirms; generation (sub-step 3) turns the confirmed plan into creatives + scheduled posts.
/// Reads the catalog only through <see cref="ICatalogReader"/> (extraction seam).
/// </summary>
public sealed class MarketingPlanService(
    EcommerceDbContext db, IMarketingPlanSettingsService settingsService, ICatalogReader catalog)
    : IMarketingPlanService
{
    private static readonly string[] TextIdeas =
    {
        "Share a customer favourite and why people love it",
        "Behind the scenes of your store",
        "A quick tip your customers will find useful",
        "Answer a question customers often ask",
        "Show what's new in store this week",
    };

    public async Task<PlanDto> ProposeAsync(ProposePlanRequest req, CancellationToken ct = default)
    {
        var settings = await settingsService.GetAsync(ct);
        var weekStart = ResolveWeekStart(req.WeekStart, settings.WeekStartDay);
        var now = DateTime.UtcNow;

        // Replace any existing draft — "propose" gives a fresh week.
        var oldDrafts = await db.MarketingPlans.Where(p => p.Status == "draft").ToListAsync(ct);
        if (oldDrafts.Count > 0)
        {
            var ids = oldDrafts.Select(p => p.MarketingPlanId).ToList();
            var oldItems = await db.MarketingPlanItems.Where(i => ids.Contains(i.MarketingPlanId)).ToListAsync(ct);
            db.MarketingPlanItems.RemoveRange(oldItems);
            db.MarketingPlans.RemoveRange(oldDrafts);
        }

        var plan = new MarketingPlan { WeekStart = weekStart, Status = "draft", CreatedAt = now };
        db.MarketingPlans.Add(plan);

        var textChannels = settings.Channels.Where(c => c.Enabled && c.AllowText).Select(c => c.Platform).ToList();
        var posterChannels = settings.Channels.Where(c => c.Enabled && c.AllowPoster).Select(c => c.Platform).ToList();

        var products = await catalog.TopProductsAsync(Math.Max(8, settings.PostersPerWeek + settings.TextPerWeek), ct);
        var festivals = await db.GrowthFestivals.AsNoTracking()
            .Where(f => f.Date >= weekStart && f.Date < weekStart.AddDays(7))
            .OrderBy(f => f.Date).ToListAsync(ct);

        // Build the intent list: posters first (product/festival led), then text (rotating ideas).
        var drafts = new List<MarketingPlanItem>();
        for (var i = 0; i < settings.PostersPerWeek; i++)
        {
            var (topic, angle, productId) = PosterSubject(i, products, festivals);
            drafts.Add(new MarketingPlanItem { Type = "poster", Topic = topic, Angle = angle, ProductId = productId, Channels = string.Join(",", posterChannels) });
        }
        for (var i = 0; i < settings.TextPerWeek; i++)
        {
            var (topic, angle, productId) = TextSubject(i, products, festivals);
            drafts.Add(new MarketingPlanItem { Type = "text", Topic = topic, Angle = angle, ProductId = productId, Channels = string.Join(",", textChannels) });
        }

        // Spread across the 7 days at the default hour; stamp tenant + status + order.
        for (var k = 0; k < drafts.Count; k++)
        {
            var day = drafts.Count <= 1 ? 0 : (int)Math.Floor(k * 7.0 / drafts.Count);
            var item = drafts[k];
            item.ScheduledAt = weekStart.AddDays(Math.Clamp(day, 0, 6)).AddHours(settings.DefaultPostHour);
            item.Status = "proposed";
            item.SortOrder = k;
            item.CreatedAt = now;
            item.Plan = plan;
            db.MarketingPlanItems.Add(item);
        }

        await db.SaveChangesAsync(ct);
        return await LoadAsync(plan.MarketingPlanId, ct) ?? throw new AppException("Failed to create plan.", StatusCodes.Status500InternalServerError);
    }

    public async Task<PlanDto?> GetCurrentAsync(CancellationToken ct = default)
    {
        var plan = await db.MarketingPlans.AsNoTracking()
            .Where(p => p.Status != "done")
            .OrderByDescending(p => p.MarketingPlanId)
            .FirstOrDefaultAsync(ct);
        return plan is null ? null : await LoadAsync(plan.MarketingPlanId, ct);
    }

    public async Task<PlanItemDto> UpdateItemAsync(long itemId, UpdatePlanItemRequest req, CancellationToken ct = default)
    {
        var item = await LoadEditableItemAsync(itemId, ct);
        if (req.ScheduledAt is { } at) item.ScheduledAt = at;
        if (req.Topic is not null) item.Topic = Clean(req.Topic, 300) ?? item.Topic;
        if (req.Angle is not null) item.Angle = Clean(req.Angle, 500);
        if (req.Channels is not null) item.Channels = JoinChannels(req.Channels);
        if (req.IncludeLogo is { } il) item.IncludeLogo = il;
        if (req.IncludeName is { } inm) item.IncludeName = inm;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(item);
    }

    public async Task<PlanItemDto> AddItemAsync(AddPlanItemRequest req, CancellationToken ct = default)
    {
        var plan = await db.MarketingPlans.Where(p => p.Status == "draft").OrderByDescending(p => p.MarketingPlanId).FirstOrDefaultAsync(ct)
            ?? throw new AppException("No draft plan to add to. Propose a plan first.", StatusCodes.Status409Conflict);
        var type = req.Type?.ToLowerInvariant() == "poster" ? "poster" : "text";
        var maxOrder = await db.MarketingPlanItems.Where(i => i.MarketingPlanId == plan.MarketingPlanId).MaxAsync(i => (int?)i.SortOrder, ct) ?? -1;
        var item = new MarketingPlanItem
        {
            MarketingPlanId = plan.MarketingPlanId,
            Type = type,
            ProductId = req.ProductId,
            Topic = Clean(req.Topic, 300) ?? "Untitled post",
            Channels = JoinChannels(req.Channels ?? []),
            ScheduledAt = req.ScheduledAt,
            Status = "proposed",
            SortOrder = maxOrder + 1,
            CreatedAt = DateTime.UtcNow,
            Plan = plan,
        };
        db.MarketingPlanItems.Add(item);
        await db.SaveChangesAsync(ct);
        return Map(item);
    }

    public async Task RemoveItemAsync(long itemId, CancellationToken ct = default)
    {
        var item = await LoadEditableItemAsync(itemId, ct);
        db.MarketingPlanItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PlanDto> ConfirmAsync(long planId, CancellationToken ct = default)
    {
        var plan = await db.MarketingPlans.FirstOrDefaultAsync(p => p.MarketingPlanId == planId, ct)
            ?? throw new AppException("Plan not found.", StatusCodes.Status404NotFound);
        if (plan.Status == "draft") { plan.Status = "confirmed"; plan.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
        // Generation of creatives + scheduled posts happens in MS2 sub-step 3.
        return (await LoadAsync(planId, ct))!;
    }

    public async Task DiscardAsync(long planId, CancellationToken ct = default)
    {
        var plan = await db.MarketingPlans.FirstOrDefaultAsync(p => p.MarketingPlanId == planId, ct);
        if (plan is null) return;
        var items = await db.MarketingPlanItems.Where(i => i.MarketingPlanId == planId).ToListAsync(ct);
        db.MarketingPlanItems.RemoveRange(items);
        db.MarketingPlans.Remove(plan);
        await db.SaveChangesAsync(ct);
    }

    // ---- subject heuristics (deterministic; the LLM authors real copy at generation time) ----

    private static (string topic, string? angle, long? productId) PosterSubject(int i, IReadOnlyList<CatalogProduct> products, IReadOnlyList<GrowthFestival> festivals)
    {
        if (i == 0 && festivals.Count > 0)
        {
            var f = festivals[0];
            return ($"{f.Name} offer", f.SuggestedGoal, null);
        }
        if (products.Count > 0)
        {
            var p = products[i % products.Count];
            return ($"Spotlight: {p.Name}", $"Feature {p.Name} (₹{p.Price:0}) with a strong call to action", p.ProductId);
        }
        return ("New this week", "Showcase what's new in the store", null);
    }

    private static (string topic, string? angle, long? productId) TextSubject(int i, IReadOnlyList<CatalogProduct> products, IReadOnlyList<GrowthFestival> festivals)
    {
        if (i == 0 && festivals.Count > (i))
        {
            var f = festivals[Math.Min(i, festivals.Count - 1)];
            return ($"{f.Name} greeting + offer", f.SuggestedGoal, null);
        }
        // Alternate a product mention with a generic idea.
        if (i % 2 == 1 && products.Count > 0)
        {
            var p = products[(i / 2) % products.Count];
            return ($"Talk about {p.Name}", $"A short, friendly post about {p.Name}", p.ProductId);
        }
        return (TextIdeas[i % TextIdeas.Length], null, null);
    }

    // ---- helpers ----

    private DateTime ResolveWeekStart(DateTime? given, int weekStartDay)
    {
        var basis = (given ?? DateTime.UtcNow).Date;
        if (given is null)
        {
            // Next occurrence of the week-start weekday (today if today is it) — forward-looking.
            var diff = (weekStartDay - (int)basis.DayOfWeek + 7) % 7;
            return basis.AddDays(diff);
        }
        // Start of the week containing the given date.
        var back = ((int)basis.DayOfWeek - weekStartDay + 7) % 7;
        return basis.AddDays(-back);
    }

    private async Task<MarketingPlanItem> LoadEditableItemAsync(long itemId, CancellationToken ct)
    {
        var item = await db.MarketingPlanItems.Include(i => i.Plan).FirstOrDefaultAsync(i => i.MarketingPlanItemId == itemId, ct)
            ?? throw new AppException("Item not found.", StatusCodes.Status404NotFound);
        if (item.Plan is not null && item.Plan.Status != "draft")
            throw new AppException("This plan is already confirmed and can't be edited.", StatusCodes.Status409Conflict);
        return item;
    }

    private async Task<PlanDto?> LoadAsync(long planId, CancellationToken ct)
    {
        var plan = await db.MarketingPlans.AsNoTracking().FirstOrDefaultAsync(p => p.MarketingPlanId == planId, ct);
        if (plan is null) return null;
        var items = await db.MarketingPlanItems.AsNoTracking()
            .Where(i => i.MarketingPlanId == planId)
            .OrderBy(i => i.ScheduledAt).ThenBy(i => i.SortOrder)
            .ToListAsync(ct);
        return new PlanDto(plan.MarketingPlanId, plan.WeekStart, plan.Status, items.Select(Map).ToList());
    }

    private static PlanItemDto Map(MarketingPlanItem i) => new(
        i.MarketingPlanItemId, i.ScheduledAt, i.Type, i.ProductId, i.Topic, i.Angle,
        string.IsNullOrWhiteSpace(i.Channels) ? [] : i.Channels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        i.IncludeLogo, i.IncludeName, i.Status);

    private static string JoinChannels(IReadOnlyList<string> channels) =>
        string.Join(",", channels.Where(SocialPlatforms.IsKnown).Select(c => SocialPlatforms.Get(c)!.Key).Distinct());

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
