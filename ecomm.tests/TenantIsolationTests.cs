using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// V2-0 gate: proves cross-tenant isolation. Every ITenantScoped entity is
/// filtered to the current tenant, inserts are auto-stamped, and no direct
/// lookup can leak another tenant's row. Nothing in V2 proceeds unless these pass.
/// </summary>
public class TenantIsolationTests
{
    private static Product Product(string sku) =>
        new() { Sku = sku, Name = sku, Slug = sku, Price = 100, Status = "Active" };

    [Fact]
    public async Task Products_are_isolated_and_auto_stamped()
    {
        var db = Guid.NewGuid().ToString();
        long bId;

        using (var a = TestDb.ForDatabase(db, tenantId: 1))
        {
            a.Products.Add(Product("A-1"));
            await a.SaveChangesAsync();
        }
        using (var b = TestDb.ForDatabase(db, tenantId: 2))
        {
            var p = Product("B-1");
            b.Products.Add(p);
            await b.SaveChangesAsync();
            bId = p.ProductId;
        }

        using var asA = TestDb.ForDatabase(db, tenantId: 1);
        var aProducts = await asA.Products.ToListAsync();
        Assert.Single(aProducts);
        Assert.Equal("A-1", aProducts[0].Sku);
        Assert.Equal(1, aProducts[0].TenantId);                       // auto-stamped

        // A cannot fetch B's row by id — filtered to null, never leaked
        Assert.Null(await asA.Products.FirstOrDefaultAsync(p => p.ProductId == bId));

        using var asB = TestDb.ForDatabase(db, tenantId: 2);
        var bProducts = await asB.Products.ToListAsync();
        Assert.Single(bProducts);
        Assert.Equal("B-1", bProducts[0].Sku);
        Assert.Equal(2, bProducts[0].TenantId);

        // Super-admin path (IgnoreQueryFilters) sees both — proves the data exists but is filtered
        Assert.Equal(2, await asA.Products.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Insert_is_stamped_with_current_tenant_even_if_set_wrong()
    {
        var db = Guid.NewGuid().ToString();
        using (var a = TestDb.ForDatabase(db, tenantId: 5))
        {
            a.Products.Add(new Product { Sku = "X", Name = "X", Slug = "x", Price = 1, TenantId = 999 });
            await a.SaveChangesAsync();
        }
        using var check = TestDb.ForDatabase(db, tenantId: 5);
        var p = await check.Products.SingleAsync();
        Assert.Equal(5, p.TenantId);   // overwritten to the current tenant, not 999
    }

    [Fact]
    public async Task Coupons_and_notifications_are_isolated_too()
    {
        var db = Guid.NewGuid().ToString();
        using (var a = TestDb.ForDatabase(db, tenantId: 1))
        {
            a.Coupons.Add(new Coupon { Code = "A10", DiscountType = "Flat", DiscountValue = 10 });
            a.Notifications.Add(new Notification { Type = "Test", Title = "A", Audience = "Admin" });
            await a.SaveChangesAsync();
        }
        using (var b = TestDb.ForDatabase(db, tenantId: 2))
        {
            b.Coupons.Add(new Coupon { Code = "B10", DiscountType = "Flat", DiscountValue = 20 });
            await b.SaveChangesAsync();
        }

        using var asA = TestDb.ForDatabase(db, tenantId: 1);
        var coupons = await asA.Coupons.ToListAsync();
        Assert.Single(coupons);
        Assert.Equal("A10", coupons[0].Code);
        Assert.Single(await asA.Notifications.ToListAsync());

        using var asB = TestDb.ForDatabase(db, tenantId: 2);
        var bCoupons = await asB.Coupons.ToListAsync();
        Assert.Single(bCoupons);
        Assert.Equal("B10", bCoupons[0].Code);
        Assert.Empty(await asB.Notifications.ToListAsync());   // B has none of A's notifications
    }

    [Fact]
    public async Task Cross_tenant_write_via_BeginScope_is_honoured()
    {
        var db = Guid.NewGuid().ToString();
        var tenant = new FixedTenant(1);
        using var ctx = TestDb.ForDatabase(db, tenant);

        // A background job / super-admin explicitly acting for tenant 7
        using (tenant.BeginScope(7))
        {
            ctx.Products.Add(Product("SEVEN"));
            await ctx.SaveChangesAsync();
        }

        var stamped = await ctx.Products.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(7, stamped.TenantId);
    }
}
