using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Growth;

public sealed record FestivalDto(long Id, string Name, DateTime Date, string Region, string SuggestedGoal, string? Note, int DaysAway);

/// <summary>One cell of the marketing calendar — either a festival occasion or a scheduled/sent campaign.</summary>
public sealed record CalendarEntryDto(string Kind, DateTime Date, string Title, string? Subtitle,
    long? CampaignId, string? Status, string? SuggestedGoal, string? Region);

public interface IGrowthCalendarService
{
    /// <summary>Upcoming festivals within N days, soonest first — drives the lead-time nudges.</summary>
    Task<IReadOnlyList<FestivalDto>> UpcomingFestivalsAsync(int withinDays, CancellationToken ct = default);
    /// <summary>Everything on the calendar between two dates: festivals (global) + this store's scheduled/sent campaigns.</summary>
    Task<IReadOnlyList<CalendarEntryDto>> CalendarAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

/// <summary>
/// The marketing calendar (G3). Festivals are global (<see cref="Data.Entities.GrowthFestival"/>, seeded
/// platform-wide); scheduled campaign sends are this tenant's own. The value is the lead-time nudge —
/// "Diwali is in 3 weeks, generate your campaign" — which global tools built outside India don't have.
/// </summary>
public sealed class GrowthCalendarService(EcommerceDbContext db) : IGrowthCalendarService
{
    public async Task<IReadOnlyList<FestivalDto>> UpcomingFestivalsAsync(int withinDays, CancellationToken ct = default)
    {
        withinDays = Math.Clamp(withinDays, 1, 400);
        var today = DateTime.UtcNow.Date;
        var until = today.AddDays(withinDays);

        var rows = await db.GrowthFestivals.AsNoTracking()
            .Where(f => f.Date >= today && f.Date <= until)
            .OrderBy(f => f.Date)
            .ToListAsync(ct);

        return rows.Select(f => new FestivalDto(
            f.GrowthFestivalId, f.Name, f.Date, f.Region, f.SuggestedGoal, f.Note, (f.Date.Date - today).Days)).ToList();
    }

    public async Task<IReadOnlyList<CalendarEntryDto>> CalendarAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var fromD = from.Date;
        var toD = to.Date;
        if (toD < fromD) (fromD, toD) = (toD, fromD);

        var festivals = await db.GrowthFestivals.AsNoTracking()
            .Where(f => f.Date >= fromD && f.Date <= toD)
            .Select(f => new CalendarEntryDto("festival", f.Date, f.Name, f.Region, null, null, f.SuggestedGoal, f.Region))
            .ToListAsync(ct);

        // This tenant's scheduled or sent campaigns landing in the window (global query filter scopes it).
        var toExclusive = toD.AddDays(1);
        var campaigns = await db.GrowthCampaigns.AsNoTracking()
            .Where(c => c.ScheduledAt != null && c.ScheduledAt >= fromD && c.ScheduledAt < toExclusive)
            .Select(c => new CalendarEntryDto("campaign", c.ScheduledAt!.Value, c.Name,
                c.Status == "Sent" ? $"Sent to {c.SentCount}" : c.Channel, c.GrowthCampaignId, c.Status, c.Goal, null))
            .ToListAsync(ct);

        return festivals.Concat(campaigns).OrderBy(e => e.Date).ToList();
    }
}
