using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Commerce;

public sealed record FunnelDto(int SessionsViewed, int SessionsAddedToCart, int Orders,
    double ViewToCartRate, double CartToOrderRate, double OverallConversion);
public sealed record TopProductRow(long ProductId, string Name, int Views);
public sealed record TopSearchRow(string Term, int Count);
public sealed record DayPoint(DateTime Date, int Views, int AddToCarts);
public sealed record NamedCount(string Name, int Count);
public sealed record TrafficDayPoint(DateTime Date, int Pageviews);
public sealed record TrafficDto(
    int Pageviews, int Sessions, int Visitors, int NewVisitors, int ReturningVisitors,
    IReadOnlyList<NamedCount> BySource, IReadOnlyList<NamedCount> ByDevice, IReadOnlyList<NamedCount> ByCountry,
    IReadOnlyList<NamedCount> ByRegion, IReadOnlyList<NamedCount> ByCity, IReadOnlyList<NamedCount> TopPages,
    IReadOnlyList<TrafficDayPoint> Series);
public sealed record StorefrontAnalyticsDto(
    int Views, int AddToCarts, int Orders, FunnelDto Funnel,
    IReadOnlyList<TopProductRow> TopViewed, IReadOnlyList<TopSearchRow> TopSearches, IReadOnlyList<DayPoint> Series,
    TrafficDto Traffic);

public interface IStorefrontAnalyticsService
{
    Task<StorefrontAnalyticsDto> GetAsync(int days, CancellationToken ct = default);
}

/// <summary>
/// Merchant storefront analytics (Phase-6 Track E) — a second consumer of the C1 behavioural-event data
/// (views/add-to-cart/search), joined with real orders for the purchase step. Gives the funnel + top
/// viewed products + top search terms that order-only reporting can't. Tenant-scoped by the query filter.
/// </summary>
public sealed class StorefrontAnalyticsService(EcommerceDbContext db) : IStorefrontAnalyticsService
{
    private static readonly string[] SoldStatuses = { "Paid", "Confirmed", "Packed", "Shipped", "Delivered" };

    public async Task<StorefrontAnalyticsDto> GetAsync(int days, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.Date.AddDays(-days + 1);

        var views = await db.CustomerEvents.Where(e => e.EventType == "view" && e.CreatedAt >= since).CountAsync(ct);
        var addToCarts = await db.CustomerEvents.Where(e => e.EventType == "add-to-cart" && e.CreatedAt >= since).CountAsync(ct);
        var orders = await db.Orders.Where(o => SoldStatuses.Contains(o.Status) && o.PlacedAt != null && o.PlacedAt >= since).CountAsync(ct);

        // Session-based funnel (distinct visitors reaching each step).
        var sessViewed = await db.CustomerEvents.Where(e => e.EventType == "view" && e.CreatedAt >= since)
            .Select(e => e.SessionId).Distinct().CountAsync(ct);
        var sessCart = await db.CustomerEvents.Where(e => e.EventType == "add-to-cart" && e.CreatedAt >= since)
            .Select(e => e.SessionId).Distinct().CountAsync(ct);
        double Rate(int n, int d) => d > 0 ? Math.Round(n * 100.0 / d, 1) : 0;
        var funnel = new FunnelDto(sessViewed, sessCart, orders,
            Rate(sessCart, sessViewed), Rate(orders, sessCart), Rate(orders, sessViewed));

        var topViewedRaw = await db.CustomerEvents
            .Where(e => e.EventType == "view" && e.ProductId != null && e.CreatedAt >= since)
            .GroupBy(e => e.ProductId!.Value)
            .Select(g => new { ProductId = g.Key, Views = g.Count() })
            .OrderByDescending(x => x.Views).Take(10).ToListAsync(ct);
        var ids = topViewedRaw.Select(x => x.ProductId).ToList();
        var names = await db.Products.Where(p => ids.Contains(p.ProductId)).ToDictionaryAsync(p => p.ProductId, p => p.Name, ct);
        var topViewed = topViewedRaw.Select(x => new TopProductRow(x.ProductId, names.GetValueOrDefault(x.ProductId, "—"), x.Views)).ToList();

        var topSearches = (await db.CustomerEvents
            .Where(e => e.EventType == "search" && e.Metadata != null && e.CreatedAt >= since)
            .GroupBy(e => e.Metadata!)
            .Select(g => new { Term = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(10).ToListAsync(ct))
            .Select(x => new TopSearchRow(x.Term, x.Count)).ToList();

        var byDay = await db.CustomerEvents
            .Where(e => (e.EventType == "view" || e.EventType == "add-to-cart") && e.CreatedAt >= since)
            .GroupBy(e => new { D = e.CreatedAt.Date, e.EventType })
            .Select(g => new { g.Key.D, g.Key.EventType, Count = g.Count() })
            .ToListAsync(ct);
        var series = Enumerable.Range(0, days).Select(i =>
        {
            var d = since.AddDays(i);
            return new DayPoint(d,
                byDay.Where(x => x.D == d && x.EventType == "view").Sum(x => x.Count),
                byDay.Where(x => x.D == d && x.EventType == "add-to-cart").Sum(x => x.Count));
        }).ToList();

        var traffic = await ComputeTrafficAsync(since, days, ct);
        return new StorefrontAnalyticsDto(views, addToCarts, orders, funnel, topViewed, topSearches, series, traffic);
    }

    private async Task<TrafficDto> ComputeTrafficAsync(DateTime since, int days, CancellationToken ct)
    {
        var page = db.CustomerEvents.Where(e => e.EventType == "page" && e.CreatedAt >= since);

        var pageviews = await page.CountAsync(ct);
        var visitors = await page.Select(e => e.SessionId).Distinct().CountAsync(ct);
        // Sessions ≈ distinct (visitor, day) — a simple "visits" proxy without server-side session windowing.
        var sessions = await page.Select(e => new { e.SessionId, D = e.CreatedAt.Date }).Distinct().CountAsync(ct);

        // New vs returning: a visitor is "returning" if they had any page view before the window.
        var inRange = await page.Select(e => e.SessionId).Distinct().ToListAsync(ct);
        var seenBefore = (await db.CustomerEvents.Where(e => e.EventType == "page" && e.CreatedAt < since)
            .Select(e => e.SessionId).Distinct().ToListAsync(ct)).ToHashSet();
        var returning = inRange.Count(s => seenBefore.Contains(s));
        var newVisitors = inRange.Count - returning;

        async Task<List<NamedCount>> TopAsync(System.Linq.Expressions.Expression<Func<Data.Entities.CustomerEvent, string?>> sel, int take, string emptyLabel)
        {
            var rows = await page.GroupBy(sel)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(take).ToListAsync(ct);
            return rows.Select(x => new NamedCount(string.IsNullOrWhiteSpace(x.Name) ? emptyLabel : x.Name!, x.Count)).ToList();
        }

        var bySource = await TopAsync(e => e.Referrer, 8, "Direct");
        var byDevice = await TopAsync(e => e.Device, 5, "unknown");
        var byCountry = await TopAsync(e => e.Country, 8, "unknown");
        var byRegion = await TopAsync(e => e.Region, 8, "unknown");
        var byCity = await TopAsync(e => e.City, 8, "unknown");
        var topPages = await TopAsync(e => e.Path, 10, "/");

        var byDay = await page.GroupBy(e => e.CreatedAt.Date).Select(g => new { D = g.Key, Count = g.Count() }).ToListAsync(ct);
        var trafficSeries = Enumerable.Range(0, days).Select(i =>
        {
            var d = since.AddDays(i);
            return new TrafficDayPoint(d, byDay.Where(x => x.D == d).Sum(x => x.Count));
        }).ToList();

        return new TrafficDto(pageviews, sessions, visitors, newVisitors, returning,
            bySource, byDevice, byCountry, byRegion, byCity, topPages, trafficSeries);
    }
}
