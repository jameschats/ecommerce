using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Orders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// Guest order lookup (C2). Order numbers are ORD{yyyyMMdd}-{id:D5} and therefore enumerable, so
/// these tests are mostly about what the endpoint refuses to confirm and refuses to return.
/// </summary>
public class OrderLookupTests
{
    private static (EcommerceDbContext db, OrderLookupService svc) Setup()
    {
        var db = TestDb.New(tenantId: 1);
        db.Users.Add(new User
        {
            UserId = 5, Email = "priya@example.com", NormalizedEmail = "PRIYA@EXAMPLE.COM",
            FullName = "Priya", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        db.Orders.Add(new Order
        {
            OrderId = 60, TenantId = 1, UserId = 5, OrderNumber = "ORD20260722-00060",
            Status = "Shipped", TotalAmount = 2499m, PlacedAt = DateTime.UtcNow.AddDays(-3),
            CreatedAt = DateTime.UtcNow.AddDays(-3),
        });
        db.SaveChanges();
        return (db, new OrderLookupService(db));
    }

    [Fact]
    public async Task Correct_order_number_and_email_returns_tracking_detail()
    {
        var (db, svc) = Setup();
        using var _ = db;
        db.Shipments.Add(new Shipment
        {
            ShipmentId = 11, TenantId = 1, OrderId = 60, Provider = "Shiprocket", Courier = "Delhivery",
            TrackingNumber = "AWB900", Status = "InTransit", CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await svc.FindAsync("ORD20260722-00060", "priya@example.com");

        Assert.NotNull(result);
        Assert.Equal("Shipped", result!.status);
        Assert.Equal("Delhivery", result.courier);
        Assert.Equal("AWB900", result.trackingNumber);
    }

    [Fact]
    public async Task Email_matching_is_case_insensitive()
    {
        var (db, svc) = Setup();
        using var _ = db;

        Assert.NotNull(await svc.FindAsync("ORD20260722-00060", "  PRIYA@Example.COM  "));
    }

    [Fact]
    public async Task A_wrong_email_is_indistinguishable_from_a_missing_order()
    {
        var (db, svc) = Setup();
        using var _ = db;

        // Real order number, wrong email — must NOT confirm the order exists.
        var wrongEmail = await svc.FindAsync("ORD20260722-00060", "attacker@example.com");
        // Order number that doesn't exist at all.
        var noOrder = await svc.FindAsync("ORD20260722-99999", "attacker@example.com");

        Assert.Null(wrongEmail);
        Assert.Null(noOrder);
    }

    [Theory]
    [InlineData("", "priya@example.com")]
    [InlineData("ORD20260722-00060", "")]
    [InlineData("", "")]
    public async Task Missing_input_returns_nothing(string orderNumber, string email)
    {
        var (db, svc) = Setup();
        using var _ = db;

        Assert.Null(await svc.FindAsync(orderNumber, email));
    }

    [Fact]
    public async Task Draft_and_test_orders_are_not_lookupable()
    {
        var (db, svc) = Setup();
        using var _ = db;
        db.Orders.Add(new Order
        {
            OrderId = 61, TenantId = 1, UserId = 5, OrderNumber = "DRAFT-00061",
            Status = "Draft", CreatedAt = DateTime.UtcNow,
        });
        db.Orders.Add(new Order
        {
            OrderId = 62, TenantId = 1, UserId = 5, OrderNumber = "ORD20260722-00062",
            Status = "Confirmed", IsTest = true, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.Null(await svc.FindAsync("DRAFT-00061", "priya@example.com"));
        Assert.Null(await svc.FindAsync("ORD20260722-00062", "priya@example.com"));
    }

    [Fact]
    public async Task The_payload_carries_no_address_total_or_customer_detail()
    {
        var (db, svc) = Setup();
        using var _ = db;

        var result = await svc.FindAsync("ORD20260722-00060", "priya@example.com");

        // The DTO is the guarantee — assert its shape rather than trusting the caller to be careful.
        var props = typeof(OrderLookupDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("totalAmount", props, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("shippingAddress", props, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("items", props, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("customerName", props, StringComparer.OrdinalIgnoreCase);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Timeline_includes_courier_scans_but_never_internal_notes()
    {
        var (db, svc) = Setup();
        using var _ = db;
        db.Shipments.Add(new Shipment
        {
            ShipmentId = 11, TenantId = 1, OrderId = 60, Provider = "Shiprocket",
            TrackingNumber = "AWB900", Status = "InTransit", CreatedAt = DateTime.UtcNow,
        });
        db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = 60, FromStatus = "Paid", ToStatus = "Shipped",
            Notes = "internal: chased warehouse twice", CreatedAt = DateTime.UtcNow.AddDays(-1),
        });
        db.ShipmentCheckpoints.Add(new ShipmentCheckpoint
        {
            ShipmentId = 11, TenantId = 1, RawStatus = "Out for delivery", MappedStatus = "InTransit",
            Location = "Chennai", CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await svc.FindAsync("ORD20260722-00060", "priya@example.com");

        Assert.Equal(2, result!.timeline.Count);
        Assert.All(result.timeline, e => Assert.Null(e.note));   // notes deliberately withheld
        Assert.Contains(result.timeline, e => e.location == "Chennai");
    }
}
