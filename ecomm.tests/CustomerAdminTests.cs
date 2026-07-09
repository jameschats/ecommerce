using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class CustomerAdminTests
{
    private static async Task<(EcommerceDbContext db, CustomerAdminService svc)> SetupAsync()
    {
        var db = TestDb.New(tenantId: 1);
        db.Roles.Add(new Role { RoleId = 1, TenantId = 1, Name = "Customer", NormalizedName = "CUSTOMER" });
        await db.SaveChangesAsync();
        return (db, new CustomerAdminService(db));
    }

    [Fact]
    public async Task Create_then_list_shows_customer_with_order_aggregates()
    {
        var (db, svc) = await SetupAsync();
        using var _ = db;

        var created = await svc.CreateAsync(new CreateCustomerRequest(
            "Asha K", "asha@example.com", "+919000000001", true, false, false, "vip buyer", "vip, wholesale"));

        // Two sold orders + one cancelled (must be excluded from spend).
        db.Orders.Add(new Order { UserId = created.UserId, OrderNumber = "O1", Status = "Delivered", PlacedAt = DateTime.UtcNow, TotalAmount = 500m });
        db.Orders.Add(new Order { UserId = created.UserId, OrderNumber = "O2", Status = "Paid", PlacedAt = DateTime.UtcNow, TotalAmount = 300m });
        db.Orders.Add(new Order { UserId = created.UserId, OrderNumber = "O3", Status = "Cancelled", PlacedAt = DateTime.UtcNow, TotalAmount = 999m });
        await db.SaveChangesAsync();

        var list = await svc.ListAsync(null, null, 1, 20);
        var row = Assert.Single(list.Items);
        Assert.Equal(created.UserId, row.UserId);
        Assert.Equal(2, row.OrderCount);
        Assert.Equal(800m, row.TotalSpent);          // cancelled order excluded
        Assert.True(row.AcceptsEmailMarketing);

        var segments = await svc.SegmentsAsync();
        Assert.Equal(1, segments.Single(s => s.Key == "paid").Count);
        Assert.Equal(1, segments.Single(s => s.Key == "repeat").Count);
        Assert.Equal(1, segments.Single(s => s.Key == "subscribers").Count);
        Assert.Equal(0, segments.Single(s => s.Key == "prospect").Count);
    }

    [Fact]
    public async Task Create_without_email_or_phone_is_rejected()
    {
        var (db, svc) = await SetupAsync();
        using var _ = db;
        await Assert.ThrowsAsync<ecomm.api.Common.Exceptions.AppException>(
            () => svc.CreateAsync(new CreateCustomerRequest("No Contact", null, null, false, false, false, null, null)));
    }

    [Fact]
    public async Task Prospect_segment_filters_customers_with_no_sold_orders()
    {
        var (db, svc) = await SetupAsync();
        using var _ = db;

        await svc.CreateAsync(new CreateCustomerRequest("Buyer", "b@x.com", null, false, false, false, null, null));
        var prospect = await svc.CreateAsync(new CreateCustomerRequest("Prospect", "p@x.com", null, false, false, false, null, null));
        db.Orders.Add(new Order { UserId = (await svc.ListAsync("b@x.com", null, 1, 20)).Items[0].UserId, OrderNumber = "O1", Status = "Paid", PlacedAt = DateTime.UtcNow, TotalAmount = 100m });
        await db.SaveChangesAsync();

        var prospects = await svc.ListAsync(null, "prospect", 1, 20);
        var row = Assert.Single(prospects.Items);
        Assert.Equal(prospect.UserId, row.UserId);
    }
}
