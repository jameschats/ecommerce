using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Plans;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// Plan limits (G0). MaxProducts, MaxOrders and Features were stored, advertised on the pricing
/// page and enforced nowhere — every store had unlimited everything. These lock in the enforcement
/// and, just as importantly, the two places it deliberately does NOT bite.
/// </summary>
public class EntitlementTests
{
    private static (EcommerceDbContext db, EntitlementService svc) Setup(
        int? maxProducts = null, int? maxOrders = null, string? features = null, bool withPlan = true)
    {
        var db = TestDb.New(tenantId: 1);
        if (withPlan)
        {
            db.Plans.Add(new Plan
            {
                PlanId = 1, Name = "Starter", Slug = "starter", MonthlyPrice = 499m,
                MaxProducts = maxProducts, MaxOrders = maxOrders, Features = features, IsActive = true,
            });
            db.TenantSubscriptions.Add(new TenantSubscription
            {
                TenantId = 1, PlanId = 1, Status = "Active",
                CurrentPeriodStart = DateTime.UtcNow.AddDays(-5), CurrentPeriodEnd = DateTime.UtcNow.AddDays(25),
                CreatedAt = DateTime.UtcNow,
            });
            db.SaveChanges();
        }
        return (db, new EntitlementService(db));
    }

    private static void AddProducts(EcommerceDbContext db, int count)
    {
        for (var i = 0; i < count; i++)
            db.Products.Add(new Product
            {
                TenantId = 1, Name = $"P{i}", Slug = $"p{i}", Sku = $"SKU{i}",
                Price = 10m, CreatedAt = DateTime.UtcNow,
            });
        db.SaveChanges();
    }

    [Fact]
    public async Task Adding_within_the_limit_is_allowed()
    {
        var (db, svc) = Setup(maxProducts: 5);
        using var _ = db;
        AddProducts(db, 4);

        await svc.EnsureCanAddProductsAsync();   // does not throw
        Assert.Equal(1, await svc.RemainingProductSlotsAsync());
    }

    [Fact]
    public async Task Exceeding_the_limit_is_refused_with_402_and_names_the_plan()
    {
        var (db, svc) = Setup(maxProducts: 5);
        using var _ = db;
        AddProducts(db, 5);

        var ex = await Assert.ThrowsAsync<AppException>(() => svc.EnsureCanAddProductsAsync());
        Assert.Equal(402, ex.StatusCode);
        Assert.Contains("Starter", ex.Message);
        Assert.Equal(0, await svc.RemainingProductSlotsAsync());
    }

    [Fact]
    public async Task A_null_limit_means_unlimited()
    {
        var (db, svc) = Setup(maxProducts: null);
        using var _ = db;
        AddProducts(db, 50);

        await svc.EnsureCanAddProductsAsync(100);   // does not throw
        Assert.Null(await svc.RemainingProductSlotsAsync());
    }

    [Fact]
    public async Task A_store_already_over_its_limit_is_never_broken_only_blocked_from_adding()
    {
        var (db, svc) = Setup(maxProducts: 3);
        using var _ = db;
        AddProducts(db, 10);   // e.g. the plan was downgraded, or limits were introduced later

        // Existing data is untouched and still readable…
        var usage = await svc.GetUsageAsync();
        Assert.Equal(10, usage.Products);
        Assert.Equal(3, usage.MaxProducts);
        // …but no more can be added.
        await Assert.ThrowsAsync<AppException>(() => svc.EnsureCanAddProductsAsync());
    }

    [Fact]
    public async Task A_tenant_with_no_subscription_is_unrestricted()
    {
        var (db, svc) = Setup(withPlan: false);
        using var _ = db;
        AddProducts(db, 3);

        await svc.EnsureCanAddProductsAsync(1000);   // does not throw
        Assert.Null((await svc.GetUsageAsync()).PlanName);
    }

    [Fact]
    public async Task Orders_are_counted_for_the_period_but_never_block()
    {
        var (db, svc) = Setup(maxOrders: 1);
        using var _ = db;
        db.Orders.Add(new Order { TenantId = 1, UserId = 1, OrderNumber = "A", Status = "Confirmed", PlacedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        db.Orders.Add(new Order { TenantId = 1, UserId = 1, OrderNumber = "B", Status = "Confirmed", PlacedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        // Excluded: a test order and a draft.
        db.Orders.Add(new Order { TenantId = 1, UserId = 1, OrderNumber = "C", Status = "Confirmed", PlacedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, IsTest = true });
        db.Orders.Add(new Order { TenantId = 1, UserId = 1, OrderNumber = "D", Status = "Draft", PlacedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var usage = await svc.GetUsageAsync();

        Assert.Equal(2, usage.OrdersThisPeriod);   // over the limit of 1…
        Assert.Equal(1, usage.MaxOrders);
        // …and there is deliberately no EnsureCanTakeOrder — refusing a shopper's checkout over a
        // billing ceiling would cost the merchant revenue, which is not ours to do.
        Assert.Null(typeof(IEntitlementService).GetMethod("EnsureCanTakeOrderAsync"));
    }

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("", new string[0])]
    [InlineData("not json", new string[0])]
    [InlineData("[\"growth\",\"priority\"]", new[] { "growth", "priority" })]
    [InlineData("{\"growth\":true,\"beta\":false}", new[] { "growth" })]
    public void Features_json_is_parsed_leniently_and_fails_closed(string? json, string[] expected)
    {
        Assert.Equal(expected, EntitlementService.ParseFeatures(json));
    }

    [Fact]
    public async Task Feature_checks_are_case_insensitive_and_unknown_keys_are_false()
    {
        var (db, svc) = Setup(features: "[\"growth\"]");
        using var _ = db;

        Assert.True(await svc.HasFeatureAsync("growth"));
        Assert.True(await svc.HasFeatureAsync("GROWTH"));
        Assert.False(await svc.HasFeatureAsync("video"));
        Assert.False(await svc.HasFeatureAsync(""));
    }
}
