using ecomm.api.Data.Entities;
using ecomm.api.Features.Analytics;
using Xunit;

namespace ecomm.tests;

public class AnalyticsFunnelTests
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly DateTime From = Now.AddDays(-10);
    private static readonly DateTime To = Now.AddDays(1);

    private static Cart CartWithItems(long id, string status, long? userId, decimal unit, int qty, DateTime when) => new()
    {
        CartId = id, TenantId = 1, UserId = userId, Status = status, CreatedAt = when, UpdatedAt = when,
        Items = { new CartItem { CartItemId = id * 10, CartId = id, ProductId = 1, Quantity = qty, UnitPrice = unit, CreatedAt = when } },
    };

    [Fact]
    public async Task Funnel_counts_carts_orders_paid_with_conversion_steps()
    {
        using var db = TestDb.New(tenantId: 1);
        // 3 carts with items in range (1 converted, 2 active) + 1 empty cart (excluded).
        db.Carts.Add(CartWithItems(1, "Converted", 1, 100, 1, Now.AddDays(-1)));
        db.Carts.Add(CartWithItems(2, "Active", 2, 100, 1, Now.AddDays(-1)));
        db.Carts.Add(CartWithItems(3, "Active", null, 100, 1, Now.AddDays(-1)));
        db.Carts.Add(new Cart { CartId = 4, TenantId = 1, Status = "Active", CreatedAt = Now.AddDays(-1) }); // no items
        // Orders in range: Paid + Pending count as "placed"; Draft is excluded; only Paid is "paid".
        db.Orders.Add(new Order { OrderId = 1, TenantId = 1, UserId = 1, Status = "Paid", PlacedAt = Now.AddDays(-1) });
        db.Orders.Add(new Order { OrderId = 2, TenantId = 1, UserId = 2, Status = "Pending", PlacedAt = Now.AddDays(-1) });
        db.Orders.Add(new Order { OrderId = 3, TenantId = 1, UserId = 2, Status = "Draft", PlacedAt = Now.AddDays(-1) });
        await db.SaveChangesAsync();

        var funnel = await new AnalyticsService(db).FunnelAsync(From, To);

        Assert.Equal(3, funnel.Stages.Count);
        Assert.Equal(3, funnel.Stages[0].Count);   // carts with items
        Assert.Equal(2, funnel.Stages[1].Count);   // orders placed (Draft excluded)
        Assert.Equal(1, funnel.Stages[2].Count);   // paid
        Assert.Equal(100m, funnel.Stages[0].StepPct);
        Assert.Equal(66.7m, funnel.Stages[1].StepPct);  // 2/3
        Assert.Equal(50m, funnel.Stages[2].StepPct);    // 1/2
        Assert.Equal(33.3m, funnel.Stages[2].PctOfTop); // 1/3
    }

    [Fact]
    public async Task Abandoned_lists_active_carts_with_items_biggest_first_and_names_the_customer()
    {
        using var db = TestDb.New(tenantId: 1);
        db.Users.Add(new User { UserId = 5, TenantId = 1, FullName = "Alice", Email = "alice@x.com" });
        db.Carts.Add(CartWithItems(1, "Active", 5, 500, 2, Now.AddDays(-1)));    // Alice, ₹1000
        db.Carts.Add(CartWithItems(2, "Active", null, 1000, 2, Now.AddDays(-1))); // Guest, ₹2000
        db.Carts.Add(CartWithItems(3, "Converted", 5, 999, 1, Now.AddDays(-1)));  // excluded: converted
        db.Carts.Add(new Cart { CartId = 4, TenantId = 1, Status = "Active", CreatedAt = Now.AddDays(-1) }); // excluded: empty
        db.Carts.Add(CartWithItems(5, "Active", 5, 700, 1, Now.AddDays(-40)));    // excluded: outside range
        await db.SaveChangesAsync();

        var rows = await new AnalyticsService(db).AbandonedCartsAsync(From, To);

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].CartId);           // biggest basket first
        Assert.Equal(2000m, rows[0].Value);
        Assert.Equal("Guest", rows[0].Customer);
        Assert.Equal("Alice", rows[1].Customer);
        Assert.Equal(1000m, rows[1].Value);
    }
}
