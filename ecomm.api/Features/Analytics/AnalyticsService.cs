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
public sealed record GroupProfitRow(string Name, decimal Revenue, decimal Cost, decimal Profit, decimal MarginPct);

/// <summary>
/// One period of trading. Revenue is the order total actually billed — including packing
/// charges and rounding — not the sum of line totals, because this report answers "what did
/// we take" rather than "what did the goods come to".
/// </summary>
public sealed record SalesPeriodRow(
    string Period, string Label, int Orders, int Units,
    decimal Revenue, decimal Cost, decimal Profit, decimal MarginPct, bool CostMissing);

public interface IAnalyticsService
{
    Task<AnalyticsSummaryDto> SummaryAsync(CancellationToken ct = default);
    Task<List<ProductReportRow>> BestSellersAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<ProductReportRow>> MarginsAsync(DateTime from, DateTime to, bool lowFirst, CancellationToken ct = default);
    Task<List<ReturnRateRow>> ReturnRateAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<GroupProfitRow>> ProfitByCategoryAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<GroupProfitRow>> ProfitBySupplierAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<SalesPeriodRow>> SalesOverTimeAsync(DateTime from, DateTime to, string bucket, CancellationToken ct = default);
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

    private Task<List<SoldLine>> SoldLinesAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct) =>
        (from oi in _db.OrderItems
         join o in _db.Orders on oi.OrderId equals o.OrderId
         join p in _db.Products on oi.ProductId equals p.ProductId
         where o.TenantId == Tenant && SoldStatuses.Contains(o.Status) && o.PlacedAt >= startUtc && o.PlacedAt <= endUtc
         select new SoldLine(
             oi.ProductId, oi.ProductName, oi.Quantity, oi.LineTotal,
             oi.UnitCost ?? p.CostPrice,
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
            join p in _db.Products on oi.ProductId equals p.ProductId
            where o.TenantId == Tenant && SoldStatuses.Contains(o.Status)
                  && o.PlacedAt >= startUtc && o.PlacedAt <= endUtc
            select new { o.PlacedAt, oi.Quantity, Cost = oi.UnitCost ?? p.CostPrice })
            .ToListAsync(ct);

        var orderStats = orders
            .GroupBy(o => Key(o.PlacedAt!.Value, by))
            .ToDictionary(g => g.Key, g => (Orders: g.Count(), Revenue: g.Sum(x => x.TotalAmount)));

        var lineStats = lines
            .GroupBy(l => Key(l.PlacedAt!.Value, by))
            .ToDictionary(g => g.Key, g => (
                Units: g.Sum(x => x.Quantity),
                Cost: g.Sum(x => (x.Cost ?? 0m) * x.Quantity),
                CostMissing: g.Any(x => x.Cost is null)));

        var periods = by == "day"
            ? orderStats.Keys.Union(lineStats.Keys).OrderBy(k => k).ToList()
            : Periods(from, to, by);

        return periods.Select(k =>
        {
            orderStats.TryGetValue(k, out var o);
            lineStats.TryGetValue(k, out var l);
            var profit = o.Revenue - l.Cost;
            return new SalesPeriodRow(
                k, Label(k, by), o.Orders, l.Units, o.Revenue, l.Cost, profit,
                o.Revenue > 0 ? Math.Round(profit / o.Revenue * 100m, 1) : 0m,
                l.CostMissing);
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

    // ---------------- helpers (in-memory aggregation) ----------------
    private static List<ProductReportRow> ByProduct(List<SoldLine> lines) =>
        lines.GroupBy(l => new { l.ProductId, l.ProductName }).Select(g =>
        {
            var revenue = g.Sum(l => l.Revenue);
            var cost = g.Sum(l => (l.UnitCost ?? 0m) * l.Units);
            var profit = revenue - cost;
            return new ProductReportRow(g.Key.ProductId, g.Key.ProductName, g.Sum(l => l.Units),
                revenue, cost, profit, revenue > 0 ? Math.Round(profit / revenue * 100m, 1) : 0m,
                g.Any(l => l.UnitCost is null));
        }).ToList();

    private static List<GroupProfitRow> ByGroup(List<SoldLine> lines, Func<SoldLine, string> key) =>
        lines.GroupBy(key).Select(g =>
        {
            var revenue = g.Sum(l => l.Revenue);
            var cost = g.Sum(l => (l.UnitCost ?? 0m) * l.Units);
            var profit = revenue - cost;
            return new GroupProfitRow(g.Key, revenue, cost, profit, revenue > 0 ? Math.Round(profit / revenue * 100m, 1) : 0m);
        }).OrderByDescending(r => r.Profit).ToList();
}
