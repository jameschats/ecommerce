using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Analytics;

namespace ecomm.tests;

/// <summary>
/// Guards the one rule the margin reports live or die by: <b>cost comes from the line's own
/// snapshot, or it is unknown.</b>
///
/// These reports used to fall back to the product's current CostPrice when a line had no
/// snapshot. That looks harmless until a catalogue is edited in place — this shop's demo seed
/// was renamed and repriced into the real range, so product 1 went from "Wall Calendar 2026"
/// at ₹660 to "10 x 15 Art Mount Lamination" at ₹4 with a ₹2 cost. July's ₹660 sales were
/// then costed at ₹2 and the dashboard reported a ~99% margin that never happened.
///
/// The failure is quiet and it always flatters, which is exactly why it needs a test rather
/// than an eye: nobody queries a profit number that looks too good in their favour.
/// </summary>
public class AnalyticsCostingTests
{
    private static readonly DateTime Sold = new(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime From = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc);

    /// <param name="unitCost">null = an order placed before cost snapshots existed.</param>
    private static EcommerceDbContext Seed(decimal? unitCost, decimal? productCostToday)
    {
        var db = TestDb.New();
        db.Categories.Add(new Category { CategoryId = 1, TenantId = 1, Name = "Calendars", Slug = "calendars" });
        db.Products.Add(new Product
        {
            ProductId = 1, TenantId = 1, CategoryId = 1, Sku = "CAL-1",
            // The name it carries *today*, deliberately different from the sold line below.
            Name = "10 x 15 Art Mount Lamination", Slug = "art-mount",
            Price = 4m, CostPrice = productCostToday, Status = "Active",
        });
        db.Orders.Add(new Order
        {
            OrderId = 1, TenantId = 1, UserId = 1, OrderNumber = "DCS-1", Status = "Paid",
            Subtotal = 660m, TotalAmount = 660m, PlacedAt = Sold, CreatedAt = Sold,
        });
        db.OrderItems.Add(new OrderItem
        {
            OrderItemId = 1, OrderId = 1, ProductId = 1,
            ProductName = "Wall Calendar 2026",   // what it was called when it sold
            Quantity = 1, UnitPrice = 660m, UnitCost = unitCost, LineTotal = 660m, CreatedAt = Sold,
        });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Margin_report_reports_cost_unknown_when_the_line_has_no_snapshot()
    {
        // The product carries a ₹2 cost today. That cost belongs to a different item than the
        // ₹660 calendar that actually sold, so it must not be borrowed.
        using var db = Seed(unitCost: null, productCostToday: 2m);
        var rows = await new AnalyticsService(db).MarginsAsync(From, To, lowFirst: false);

        var row = Assert.Single(rows);
        Assert.True(row.CostMissing);
        Assert.Equal(660m, row.Revenue);          // revenue is known and stays exact
        Assert.Equal(0m, row.Cost);               // not ₹2
        Assert.Equal(0m, row.Profit);             // not ₹658
        Assert.Equal(0m, row.MarginPct);          // not 99.7%
    }

    [Fact]
    public async Task Margin_report_uses_the_snapshot_when_the_line_has_one()
    {
        // Cost was captured at sale time, so profit is real — even though the product's cost
        // has since been changed to something else entirely.
        using var db = Seed(unitCost: 500m, productCostToday: 2m);
        var rows = await new AnalyticsService(db).MarginsAsync(From, To, lowFirst: false);

        var row = Assert.Single(rows);
        Assert.False(row.CostMissing);
        Assert.Equal(500m, row.Cost);
        Assert.Equal(160m, row.Profit);
        Assert.Equal(24.2m, row.MarginPct);
    }

    [Fact]
    public async Task One_uncosted_line_withholds_profit_for_the_whole_group()
    {
        // A group is only as trustworthy as its worst line: counting the unknown as zero
        // understates cost, which overstates profit.
        using var db = Seed(unitCost: 500m, productCostToday: 2m);
        db.Products.Add(new Product
        {
            ProductId = 2, TenantId = 1, CategoryId = 1, Sku = "CAL-2",
            Name = "Desk Calendar", Slug = "desk", Price = 100m, CostPrice = 60m, Status = "Active",
        });
        db.OrderItems.Add(new OrderItem
        {
            OrderItemId = 2, OrderId = 1, ProductId = 2, ProductName = "Desk Calendar",
            Quantity = 1, UnitPrice = 100m, UnitCost = null, LineTotal = 100m, CreatedAt = Sold,
        });
        await db.SaveChangesAsync();

        var group = Assert.Single(await new AnalyticsService(db).ProfitByCategoryAsync(From, To));
        Assert.Equal("Calendars", group.Name);
        Assert.Equal(760m, group.Revenue);        // both lines' revenue still counts
        Assert.True(group.CostMissing);
        Assert.Equal(0m, group.Profit);           // not ₹260 (760 − 500, the uncosted line free)
    }

    [Fact]
    public async Task Sales_over_time_withholds_profit_but_still_reports_the_takings()
    {
        using var db = Seed(unitCost: null, productCostToday: 2m);
        var rows = await new AnalyticsService(db).SalesOverTimeAsync(From, To, "month");

        var july = Assert.Single(rows, r => r.Period == "2026-07");
        Assert.Equal(1, july.Orders);
        Assert.Equal(660m, july.Revenue);         // what we took is not in doubt
        Assert.True(july.CostMissing);
        Assert.Equal(0m, july.Profit);
    }

    [Fact]
    public async Task A_month_with_no_trade_is_zero_rather_than_unknown()
    {
        // Gap-filled months exist to show quiet periods. Flagging them "cost unknown" would
        // cry wolf and train the reader to ignore the flag where it matters.
        using var db = Seed(unitCost: 500m, productCostToday: 2m);
        var rows = await new AnalyticsService(db).SalesOverTimeAsync(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), To, "month");

        var june = Assert.Single(rows, r => r.Period == "2026-06");
        Assert.Equal(0, june.Orders);
        Assert.False(june.CostMissing);
        Assert.Equal(0m, june.Revenue);
    }
}
