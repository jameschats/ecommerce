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

// ---- Sales dashboard (charts) ----
public sealed record SalesPoint(DateTime Date, decimal Sales, int Orders);
public sealed record SalesBreakdownDto(decimal Gross, decimal Discounts, decimal Returns, decimal Net, decimal Shipping, decimal Tax, decimal Total);
public sealed record NewReturningDto(int NewCustomers, int ReturningCustomers, decimal NewRevenue, decimal ReturningRevenue);
public sealed record SalesKpisDto(decimal GrossSales, decimal NetSales, int Orders, decimal Aov, decimal ReturningRatePct);
public sealed record SalesDashboardDto(
    SalesKpisDto Kpis, List<SalesPoint> Series, SalesBreakdownDto Breakdown,
    NewReturningDto NewVsReturning, List<ProductReportRow> TopProducts, List<GroupProfitRow> ByCategory);

public interface IAnalyticsService
{
    Task<AnalyticsSummaryDto> SummaryAsync(CancellationToken ct = default);
    Task<SalesDashboardDto> SalesDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<ProductReportRow>> BestSellersAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<ProductReportRow>> MarginsAsync(DateTime from, DateTime to, bool lowFirst, CancellationToken ct = default);
    Task<List<ReturnRateRow>> ReturnRateAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<GroupProfitRow>> ProfitByCategoryAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<GroupProfitRow>> ProfitBySupplierAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

public sealed class AnalyticsService : IAnalyticsService
{
    private long Tenant => _db.CurrentTenantId;
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

    // ---------------- Sales dashboard ----------------
    public async Task<SalesDashboardDto> SalesDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var sold = _db.Orders.Where(o => o.TenantId == Tenant && SoldStatuses.Contains(o.Status) && o.PlacedAt >= from && o.PlacedAt <= to);
        var returned = _db.Orders.Where(o => o.TenantId == Tenant && o.Status == "Returned" && o.PlacedAt >= from && o.PlacedAt <= to);

        // Sales over time (by day).
        var rawSeries = await sold
            .GroupBy(o => o.PlacedAt!.Value.Date)
            .Select(g => new { Date = g.Key, Sales = g.Sum(o => o.TotalAmount), Orders = g.Count() })
            .ToListAsync(ct);
        var series = rawSeries.OrderBy(x => x.Date).Select(x => new SalesPoint(x.Date, x.Sales, x.Orders)).ToList();

        // Breakdown (gross → discounts → returns → net; + shipping + tax → total).
        var gross = await sold.SumAsync(o => (decimal?)o.Subtotal, ct) ?? 0m;
        var discounts = await sold.SumAsync(o => (decimal?)o.DiscountAmount, ct) ?? 0m;
        var shipping = await sold.SumAsync(o => (decimal?)o.ShippingAmount, ct) ?? 0m;
        var tax = await sold.SumAsync(o => (decimal?)o.TaxAmount, ct) ?? 0m;
        var soldTotal = await sold.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
        var returnsAmt = await returned.SumAsync(o => (decimal?)o.Subtotal, ct) ?? 0m;
        var returnsTotal = await returned.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
        var net = gross - discounts - returnsAmt;
        var breakdown = new SalesBreakdownDto(gross, discounts, returnsAmt, net, shipping, tax, soldTotal - returnsTotal);

        var orders = await sold.CountAsync(ct);
        var aov = orders > 0 ? Math.Round(soldTotal / orders, 2) : 0m;

        // New vs returning: a customer is "new" if their FIRST sold order (all-time) falls in this range.
        var customerIds = await sold.Select(o => o.UserId).Distinct().ToListAsync(ct);
        var firstDates = await _db.Orders
            .Where(o => o.TenantId == Tenant && SoldStatuses.Contains(o.Status) && o.PlacedAt != null && customerIds.Contains(o.UserId))
            .GroupBy(o => o.UserId)
            .Select(g => new { UserId = g.Key, First = g.Min(o => o.PlacedAt!.Value) })
            .ToListAsync(ct);
        var newIds = firstDates.Where(x => x.First >= from).Select(x => x.UserId).ToHashSet();
        var revByCustomer = await sold.GroupBy(o => o.UserId)
            .Select(g => new { UserId = g.Key, Rev = g.Sum(o => o.TotalAmount) }).ToListAsync(ct);
        decimal newRev = 0m, retRev = 0m;
        int newC = 0, retC = 0;
        foreach (var c in revByCustomer)
            if (newIds.Contains(c.UserId)) { newRev += c.Rev; newC++; } else { retRev += c.Rev; retC++; }
        var newVsReturning = new NewReturningDto(newC, retC, newRev, retRev);
        var returningRate = customerIds.Count > 0 ? Math.Round((decimal)retC / customerIds.Count * 100m, 1) : 0m;

        var kpis = new SalesKpisDto(gross, net, orders, aov, returningRate);
        var topProducts = (await BestSellersAsync(from, to, ct)).Take(8).ToList();
        var byCategory = await ProfitByCategoryAsync(from, to, ct);
        return new SalesDashboardDto(kpis, series, breakdown, newVsReturning, topProducts, byCategory);
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
