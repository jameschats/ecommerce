using System.Globalization;
using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Analytics;

public sealed record TopSearchDto(string Term, long Count);
public sealed record AnalyticsSummaryDto(
    int OrdersToday, int OrdersThisWeek, decimal RevenueToday, decimal RevenueThisWeek, decimal AovThisWeek,
    int NewSignupsToday, int NewSignupsThisWeek, int LowStockCount, int PendingActionCount, List<TopSearchDto> TopSearches);

/// <summary>A product row for best-sellers / margin reports. CostMissing ⇒ some units had no cost price.</summary>
public sealed record ProductReportRow(long ProductId, string Name, int Units, decimal Revenue, decimal Cost, decimal Profit, decimal MarginPct, bool CostMissing);
public sealed record ReturnRateRow(long ProductId, string Name, int Sold, int Returned, decimal ReturnRatePct);
/// <summary>Profit for a category or supplier. CostMissing ⇒ Cost/Profit/MarginPct are not known.</summary>
public sealed record GroupProfitRow(string Name, decimal Revenue, decimal Cost, decimal Profit, decimal MarginPct, bool CostMissing);

/// <summary>
/// One period of trading. Revenue is the order total actually billed — including packing
/// charges and rounding — not the sum of line totals, because this report answers "what did
/// we take" rather than "what did the goods come to".
/// </summary>
public sealed record SalesPeriodRow(
    string Period, string Label, int Orders, int Units,
    decimal Revenue, decimal Cost, decimal Profit, decimal MarginPct, bool CostMissing);

// ---------------- Traffic (first-party, PageViews) ----------------
public sealed record TrafficSummaryDto(int Sessions, int UniqueVisitors, double SessionsChangePct, double VisitorsChangePct);
public sealed record TrafficPointDto(string Date, string Label, int Sessions);
public sealed record DeviceBreakdownDto(string Device, int Sessions, double Pct);
public sealed record SourceBreakdownDto(string Source, int Sessions, double Pct);
public sealed record TopPageDto(string Path, int Views);
public sealed record GeoBreakdownDto(string Country, string City, int Sessions);
public sealed record NewVsReturningDto(int New, int Returning);

public interface IAnalyticsService
{
    Task<AnalyticsSummaryDto> SummaryAsync(CancellationToken ct = default);
    Task<List<ProductReportRow>> BestSellersAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<ProductReportRow>> MarginsAsync(DateTime from, DateTime to, bool lowFirst, CancellationToken ct = default);
    Task<List<ReturnRateRow>> ReturnRateAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<GroupProfitRow>> ProfitByCategoryAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<GroupProfitRow>> ProfitBySupplierAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<SalesPeriodRow>> SalesOverTimeAsync(DateTime from, DateTime to, string bucket, CancellationToken ct = default);

    Task<TrafficSummaryDto> TrafficSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<TrafficPointDto>> TrafficOverTimeAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<DeviceBreakdownDto>> TrafficByDeviceAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<SourceBreakdownDto>> TrafficBySourceAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<TopPageDto>> TopPagesAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<GeoBreakdownDto>> TrafficByGeoAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<NewVsReturningDto> NewVsReturningAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

public sealed class AnalyticsService : IAnalyticsService
{
    private const long Tenant = 1;
    private static readonly string[] SoldStatuses = { "Paid", "Confirmed", "Packed", "Shipped", "Delivered" };
    private static readonly string[] LostStatuses = { "Cancelled", "Returned" };

    private readonly EcommerceDbContext _db;
    public AnalyticsService(EcommerceDbContext db) => _db = db;

    // ---------------- Activity widget ----------------
    public async Task<AnalyticsSummaryDto> SummaryAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = today.AddDays(-6);

        var sold = _db.Orders.Where(o => o.TenantId == Tenant && SoldStatuses.Contains(o.Status) && o.PlacedAt != null);
        var ordersToday = await sold.CountAsync(o => o.PlacedAt >= today, ct);
        var ordersWeek = await sold.CountAsync(o => o.PlacedAt >= weekAgo, ct);
        var revenueToday = await sold.Where(o => o.PlacedAt >= today).SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
        var revenueWeek = await sold.Where(o => o.PlacedAt >= weekAgo).SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

        var signupsToday = await _db.Users.CountAsync(u => u.TenantId == Tenant && !u.IsDeleted && u.CreatedAt >= today, ct);
        var signupsWeek = await _db.Users.CountAsync(u => u.TenantId == Tenant && !u.IsDeleted && u.CreatedAt >= weekAgo, ct);

        var lowStock = await _db.Inventory.CountAsync(i => i.AvailableQty <= i.ReorderLevel, ct);
        var pending = await _db.Orders.CountAsync(o => o.TenantId == Tenant && (o.Status == "Paid" || o.Status == "Confirmed"), ct);

        var topSearches = await _db.PopularSearches.AsNoTracking()
            .Where(p => p.TenantId == Tenant)
            .OrderByDescending(p => p.SearchCount).Take(5)
            .Select(p => new TopSearchDto(p.Term, p.SearchCount)).ToListAsync(ct);

        var aov = ordersWeek > 0 ? Math.Round(revenueWeek / ordersWeek, 2) : 0m;
        return new AnalyticsSummaryDto(ordersToday, ordersWeek, revenueToday, revenueWeek, aov,
            signupsToday, signupsWeek, lowStock, pending, topSearches);
    }

    // ---------------- Reports ----------------
    private sealed record SoldLine(long ProductId, string ProductName, int Units, decimal Revenue, decimal? UnitCost, string CategoryName, string SupplierName);

    /// <summary>
    /// Cost comes from the line's own snapshot and nowhere else. It deliberately does *not*
    /// fall back to the product's current CostPrice: products get renamed, repriced and
    /// repurposed, so today's cost may belong to a different item than the one that was sold.
    /// This catalogue was edited in place from its demo seed — order line "Wall Calendar 2026"
    /// (₹660) and product 1 "10 x 15 Art Mount Lamination" (₹4, cost ₹2) share an id and
    /// nothing else. Costing that sale at ₹2 reported a 99% margin that never happened.
    /// A line with no snapshot has an <b>unknown</b> cost, and the reports say so.
    /// </summary>
    private Task<List<SoldLine>> SoldLinesAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct) =>
        (from oi in _db.OrderItems
         join o in _db.Orders on oi.OrderId equals o.OrderId
         join p in _db.Products on oi.ProductId equals p.ProductId
         where o.TenantId == Tenant && SoldStatuses.Contains(o.Status) && o.PlacedAt >= startUtc && o.PlacedAt <= endUtc
         select new SoldLine(
             oi.ProductId, oi.ProductName, oi.Quantity, oi.LineTotal,
             oi.UnitCost,
             p.Category!.Name,
             _db.ProductSuppliers.Where(ps => ps.ProductId == oi.ProductId && ps.IsPrimary && ps.IsActive)
                 .Join(_db.Suppliers, ps => ps.SupplierId, s => s.SupplierId, (ps, s) => s.Name).FirstOrDefault() ?? "Unassigned"))
        .ToListAsync(ct);

    /// <summary>
    /// Sales grouped by day, month or year — the "total sales for the year, monthly" report.
    ///
    /// Every other report here groups by product, category or supplier; this is the only one
    /// that groups by time, and it is the one a business actually reads first.
    ///
    /// Revenue is the order total, so it includes packing charges and rounding and reconciles
    /// with the money received. Cost comes from the lines, giving a true gross profit for the
    /// period. Empty periods are emitted as zero rows for month and year, because a month
    /// with no sales is a fact worth seeing rather than a gap that quietly closes up.
    /// </summary>
    public async Task<List<SalesPeriodRow>> SalesOverTimeAsync(
        DateTime from, DateTime to, string bucket, CancellationToken ct = default)
    {
        var by = (bucket ?? "month").Trim().ToLowerInvariant();
        if (by is not ("day" or "month" or "year")) by = "month";

        var orders = await _db.Orders
            .Where(o => o.TenantId == Tenant && SoldStatuses.Contains(o.Status)
                        && o.PlacedAt >= from && o.PlacedAt <= to)
            .Select(o => new { o.PlacedAt, o.TotalAmount })
            .ToListAsync(ct);

        // Local copies: `from` is a query-expression keyword, so the parameter cannot be
        // referenced by name inside one. SoldLinesAsync names its parameters startUtc/endUtc
        // for the same reason.
        var startUtc = from;
        var endUtc = to;

        var lines = await (
            from oi in _db.OrderItems
            join o in _db.Orders on oi.OrderId equals o.OrderId
            where o.TenantId == Tenant && SoldStatuses.Contains(o.Status)
                  && o.PlacedAt >= startUtc && o.PlacedAt <= endUtc
            select new { o.PlacedAt, oi.Quantity, Cost = oi.UnitCost })
            .ToListAsync(ct);

        var orderStats = orders
            .GroupBy(o => Key(o.PlacedAt!.Value, by))
            .ToDictionary(g => g.Key, g => (Orders: g.Count(), Revenue: g.Sum(x => x.TotalAmount)));

        var lineStats = lines
            .GroupBy(l => Key(l.PlacedAt!.Value, by))
            .ToDictionary(g => g.Key, g => (
                Units: g.Sum(x => x.Quantity),
                Lines: g.Select(x => (x.Cost, x.Quantity)).ToList()));

        var periods = by == "day"
            ? orderStats.Keys.Union(lineStats.Keys).OrderBy(k => k).ToList()
            : Periods(from, to, by);

        return periods.Select(k =>
        {
            orderStats.TryGetValue(k, out var o);
            lineStats.TryGetValue(k, out var l);

            // A period that traded nothing is genuinely zero, not "cost unknown" — the
            // gap-filled months exist to show quiet periods, and flagging them would cry wolf.
            var c = o.Orders == 0
                ? (Cost: 0m, Profit: 0m, MarginPct: 0m, CostMissing: false)
                : Costing(o.Revenue, l.Lines ?? new List<(decimal?, int)>());

            return new SalesPeriodRow(
                k, Label(k, by), o.Orders, l.Units, o.Revenue, c.Cost, c.Profit, c.MarginPct, c.CostMissing);
        }).ToList();
    }

    /// <summary>Sortable period key — chosen so plain string ordering is chronological.</summary>
    private static string Key(DateTime d, string bucket) => bucket switch
    {
        "year" => d.ToString("yyyy"),
        "day" => d.ToString("yyyy-MM-dd"),
        _ => d.ToString("yyyy-MM"),
    };

    private static string Label(string key, string bucket) => bucket switch
    {
        "year" => key,
        "day" => DateTime.ParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd MMM yyyy"),
        _ => DateTime.ParseExact(key, "yyyy-MM", CultureInfo.InvariantCulture).ToString("MMM yyyy"),
    };

    /// <summary>Every period in the range, so months that sold nothing still show as zero.</summary>
    private static List<string> Periods(DateTime from, DateTime to, string bucket)
    {
        var keys = new List<string>();
        if (bucket == "year")
        {
            for (var y = from.Year; y <= to.Year; y++) keys.Add(y.ToString());
            return keys;
        }
        var cursor = new DateTime(from.Year, from.Month, 1);
        var last = new DateTime(to.Year, to.Month, 1);
        while (cursor <= last)
        {
            keys.Add(cursor.ToString("yyyy-MM"));
            cursor = cursor.AddMonths(1);
        }
        return keys;
    }

    public async Task<List<ProductReportRow>> BestSellersAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        ByProduct(await SoldLinesAsync(from, to, ct)).OrderByDescending(r => r.Revenue).Take(50).ToList();

    public async Task<List<ProductReportRow>> MarginsAsync(DateTime from, DateTime to, bool lowFirst, CancellationToken ct = default)
    {
        var rows = ByProduct(await SoldLinesAsync(from, to, ct));
        var ordered = lowFirst ? rows.OrderBy(r => r.MarginPct) : rows.OrderByDescending(r => r.MarginPct);
        return ordered.Take(50).ToList();
    }

    public async Task<List<GroupProfitRow>> ProfitByCategoryAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        ByGroup(await SoldLinesAsync(from, to, ct), l => l.CategoryName);

    public async Task<List<GroupProfitRow>> ProfitBySupplierAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        ByGroup(await SoldLinesAsync(from, to, ct), l => l.SupplierName);

    public async Task<List<ReturnRateRow>> ReturnRateAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        DateTime startUtc = from, endUtc = to;
        var raw = await (from oi in _db.OrderItems
                         join o in _db.Orders on oi.OrderId equals o.OrderId
                         where o.TenantId == Tenant && o.PlacedAt >= startUtc && o.PlacedAt <= endUtc
                         select new { oi.ProductId, oi.ProductName, o.Status, oi.Quantity }).ToListAsync(ct);
        return raw.GroupBy(x => new { x.ProductId, x.ProductName }).Select(g =>
        {
            var sold = g.Where(x => SoldStatuses.Contains(x.Status)).Sum(x => x.Quantity);
            var returned = g.Where(x => LostStatuses.Contains(x.Status)).Sum(x => x.Quantity);
            var total = sold + returned;
            return new ReturnRateRow(g.Key.ProductId, g.Key.ProductName, sold, returned,
                total > 0 ? Math.Round((decimal)returned / total * 100m, 1) : 0m);
        }).Where(r => r.Returned > 0).OrderByDescending(r => r.ReturnRatePct).ToList();
    }

    // ---------------- Traffic (first-party, PageViews) ----------------

    private sealed record RawView(string VisitorId, string SessionId, string Path, string? Referrer, string DeviceType, string? Country, string? City, DateTime CreatedAt);

    private Task<List<RawView>> ViewsAsync(DateTime from, DateTime to, CancellationToken ct) =>
        _db.PageViews.AsNoTracking()
            .Where(p => p.TenantId == Tenant && p.CreatedAt >= from && p.CreatedAt <= to)
            .Select(p => new RawView(p.VisitorId, p.SessionId, p.Path, p.Referrer, p.DeviceType, p.Country, p.City, p.CreatedAt))
            .ToListAsync(ct);

    /// <summary>Same length window immediately before `from`, for the "compared to previous period" figure.</summary>
    private static (DateTime From, DateTime To) PriorPeriod(DateTime from, DateTime to)
    {
        var span = to - from;
        return (from - span - TimeSpan.FromTicks(1), from - TimeSpan.FromTicks(1));
    }

    public async Task<TrafficSummaryDto> TrafficSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var current = await ViewsAsync(from, to, ct);
        var (prevFrom, prevTo) = PriorPeriod(from, to);
        var previous = await ViewsAsync(prevFrom, prevTo, ct);

        var sessions = current.Select(v => v.SessionId).Distinct().Count();
        var visitors = current.Select(v => v.VisitorId).Distinct().Count();
        var prevSessions = previous.Select(v => v.SessionId).Distinct().Count();
        var prevVisitors = previous.Select(v => v.VisitorId).Distinct().Count();

        return new TrafficSummaryDto(sessions, visitors, ChangePct(sessions, prevSessions), ChangePct(visitors, prevVisitors));
    }

    private static double ChangePct(int current, int previous) =>
        previous == 0 ? (current == 0 ? 0 : 100) : Math.Round((current - previous) / (double)previous * 100, 1);

    /// <summary>Always day-bucketed — the traffic chart is meant for a "last N days" window, not a year view.</summary>
    public async Task<List<TrafficPointDto>> TrafficOverTimeAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var views = await ViewsAsync(from, to, ct);
        var bySessionPerDay = views
            .GroupBy(v => v.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Select(v => v.SessionId).Distinct().Count());

        var points = new List<TrafficPointDto>();
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
        {
            bySessionPerDay.TryGetValue(d, out var sessions);
            points.Add(new TrafficPointDto(d.ToString("yyyy-MM-dd"), d.ToString("dd MMM"), sessions));
        }
        return points;
    }

    public async Task<List<DeviceBreakdownDto>> TrafficByDeviceAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var views = await ViewsAsync(from, to, ct);
        var bySession = views.GroupBy(v => v.SessionId).Select(g => g.First().DeviceType).ToList();
        var total = bySession.Count;
        return bySession.GroupBy(d => d)
            .Select(g => new DeviceBreakdownDto(g.Key, g.Count(), total == 0 ? 0 : Math.Round(g.Count() / (double)total * 100, 1)))
            .OrderByDescending(r => r.Sessions).ToList();
    }

    /// <summary>Groups a referrer URL into "Direct" (none) or its registrable host, e.g. "google.com".</summary>
    private static string SourceFromReferrer(string? referrer)
    {
        if (string.IsNullOrWhiteSpace(referrer)) return "Direct";
        return Uri.TryCreate(referrer, UriKind.Absolute, out var uri) ? uri.Host.Replace("www.", "") : "Direct";
    }

    public async Task<List<SourceBreakdownDto>> TrafficBySourceAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var views = await ViewsAsync(from, to, ct);
        var bySession = views.GroupBy(v => v.SessionId).Select(g => SourceFromReferrer(g.First().Referrer)).ToList();
        var total = bySession.Count;
        return bySession.GroupBy(s => s)
            .Select(g => new SourceBreakdownDto(g.Key, g.Count(), total == 0 ? 0 : Math.Round(g.Count() / (double)total * 100, 1)))
            .OrderByDescending(r => r.Sessions).Take(10).ToList();
    }

    public async Task<List<TopPageDto>> TopPagesAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var views = await ViewsAsync(from, to, ct);
        return views.GroupBy(v => v.Path)
            .Select(g => new TopPageDto(g.Key, g.Count()))
            .OrderByDescending(r => r.Views).Take(10).ToList();
    }

    public async Task<List<GeoBreakdownDto>> TrafficByGeoAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var views = await ViewsAsync(from, to, ct);
        return views.Where(v => v.Country != null)
            .GroupBy(v => new { Country = v.Country!, City = v.City ?? "Unknown" })
            .Select(g => new GeoBreakdownDto(g.Key.Country, g.Key.City, g.Select(v => v.SessionId).Distinct().Count()))
            .OrderByDescending(r => r.Sessions).Take(10).ToList();
    }

    /// <summary>
    /// New = this visitor's very first page view (ever) falls inside the requested period;
    /// Returning = they were already seen before it started. Classified per visitor, not
    /// per session, so a visitor who comes back twice in one period is still one "returning".
    /// </summary>
    public async Task<NewVsReturningDto> NewVsReturningAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var visitorIds = await _db.PageViews.AsNoTracking()
            .Where(p => p.TenantId == Tenant && p.CreatedAt >= from && p.CreatedAt <= to)
            .Select(p => p.VisitorId).Distinct().ToListAsync(ct);
        if (visitorIds.Count == 0) return new NewVsReturningDto(0, 0);

        var firstSeen = await _db.PageViews.AsNoTracking()
            .Where(p => p.TenantId == Tenant && visitorIds.Contains(p.VisitorId))
            .GroupBy(p => p.VisitorId)
            .Select(g => g.Min(p => p.CreatedAt))
            .ToListAsync(ct);

        var newCount = firstSeen.Count(d => d >= from);
        return new NewVsReturningDto(newCount, firstSeen.Count - newCount);
    }

    // ---------------- helpers (in-memory aggregation) ----------------

    /// <summary>
    /// Gross profit for a set of lines — or "unknown", if any line has no cost snapshot.
    ///
    /// A missing cost counted as zero understates cost and so overstates profit and margin:
    /// the error always flatters. Rather than publish a number that is wrong in the
    /// comfortable direction, the whole triple is reported as zero and flagged, and callers
    /// must read CostMissing before reading Cost, Profit or MarginPct. Revenue and units are
    /// exact either way — those are known whether or not the cost is.
    /// </summary>
    private static (decimal Cost, decimal Profit, decimal MarginPct, bool CostMissing) Costing(
        decimal revenue, IEnumerable<(decimal? UnitCost, int Units)> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0 || list.Any(l => l.UnitCost is null)) return (0m, 0m, 0m, true);

        var cost = list.Sum(l => l.UnitCost!.Value * l.Units);
        var profit = revenue - cost;
        return (cost, profit, revenue > 0 ? Math.Round(profit / revenue * 100m, 1) : 0m, false);
    }

    private static List<ProductReportRow> ByProduct(List<SoldLine> lines) =>
        lines.GroupBy(l => new { l.ProductId, l.ProductName }).Select(g =>
        {
            var revenue = g.Sum(l => l.Revenue);
            var c = Costing(revenue, g.Select(l => (l.UnitCost, l.Units)));
            return new ProductReportRow(g.Key.ProductId, g.Key.ProductName, g.Sum(l => l.Units),
                revenue, c.Cost, c.Profit, c.MarginPct, c.CostMissing);
        }).ToList();

    // Ordered by revenue, not profit: a group whose cost is unknown reports zero profit, and
    // ordering by profit would bury exactly the rows that need attention.
    private static List<GroupProfitRow> ByGroup(List<SoldLine> lines, Func<SoldLine, string> key) =>
        lines.GroupBy(key).Select(g =>
        {
            var revenue = g.Sum(l => l.Revenue);
            var c = Costing(revenue, g.Select(l => (l.UnitCost, l.Units)));
            return new GroupProfitRow(g.Key, revenue, c.Cost, c.Profit, c.MarginPct, c.CostMissing);
        }).OrderByDescending(r => r.Revenue).ToList();
}
