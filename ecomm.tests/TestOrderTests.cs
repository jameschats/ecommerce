using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Analytics;
using ecomm.api.Features.Checkout;
using ecomm.api.Features.Coupons;
using ecomm.api.Features.Inventory;
using ecomm.api.Features.Orders;
using ecomm.api.Features.Plans;
using ecomm.api.Features.Shipping.Shiprocket;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// The merchant "try a test order" walkthrough (M10b). The point of the feature is that it runs the REAL
/// pipeline, so these tests check the order is genuinely created — and, critically, that it never reaches
/// analytics, because a test order inflating a merchant's revenue would be worse than no feature at all.
/// </summary>
public class TestOrderTests
{
    private static async Task<(EcommerceDbContext db, TestOrderService svc)> SetupAsync(bool withProduct = true)
    {
        var db = TestDb.New(tenantId: 1);
        db.Settings.Add(new Setting
        {
            TenantId = 1, SettingKey = "TaxMode", SettingValue = "None",
            DataType = "string", Category = "Billing", CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new User
        {
            UserId = 5, Email = "owner@store.test", NormalizedEmail = "OWNER@STORE.TEST",
            FullName = "Owner", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        if (withProduct)
            db.Products.Add(new Product
            {
                ProductId = 9, Name = "Silk Saree", Slug = "silk-saree", Sku = "SS1",
                Price = 100m, Status = "Active", IsActive = true, CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var drafts = new DraftOrderService(db, new TaxService(db), new ShippingService(db, new NoShiprocket()),
            new CouponService(db, new ecomm.api.Features.Collections.CollectionService(db)),
            new StubInventory(), new StubInvoices(), new EntitlementService(db));
        return (db, new TestOrderService(db, drafts));
    }

    [Fact]
    public async Task Places_a_real_confirmed_order_flagged_as_test()
    {
        var (db, svc) = await SetupAsync();
        using var _ = db;

        var result = await svc.PlaceAsync(merchantUserId: 5);

        var order = await db.Orders.SingleAsync();
        Assert.True(order.IsTest);
        Assert.Equal("Confirmed", order.Status);
        Assert.NotNull(order.PlacedAt);
        Assert.StartsWith("ORD", order.OrderNumber);       // a real order number, not DRAFT-
        Assert.Equal(order.OrderId, result.OrderId);
        Assert.Equal("Silk Saree", result.ProductName);
    }

    [Fact]
    public async Task Test_order_is_excluded_from_analytics()
    {
        var (db, svc) = await SetupAsync();
        using var _ = db;

        await svc.PlaceAsync(merchantUserId: 5);

        var summary = await new AnalyticsService(db).SummaryAsync();

        Assert.Equal(0, summary.OrdersToday);
        Assert.Equal(0m, summary.RevenueToday);
        Assert.Equal(0, summary.PendingActionCount);
    }

    [Fact]
    public async Task A_real_order_still_counts_alongside_a_test_order()
    {
        var (db, svc) = await SetupAsync();
        using var _ = db;

        await svc.PlaceAsync(merchantUserId: 5);
        db.Orders.Add(new Order
        {
            TenantId = 1, UserId = 5, OrderNumber = "ORD-REAL", Status = "Confirmed",
            TotalAmount = 250m, PlacedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, IsTest = false,
        });
        await db.SaveChangesAsync();

        var summary = await new AnalyticsService(db).SummaryAsync();

        Assert.Equal(1, summary.OrdersToday);
        Assert.Equal(250m, summary.RevenueToday);
    }

    [Fact]
    public async Task Without_a_product_it_asks_the_merchant_to_add_one()
    {
        var (db, svc) = await SetupAsync(withProduct: false);
        using var _ = db;

        var ex = await Assert.ThrowsAsync<ecomm.api.Common.Exceptions.AppException>(() => svc.PlaceAsync(5));
        Assert.Contains("Add a product", ex.Message);
    }

    // Convert reserves + commits stock and invoices; these record rather than throw.
    private sealed class StubInventory : IInventoryService
    {
        public Task<PagedResult<InventoryRowDto>> ListAsync(InventoryQuery q, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<InventoryRowDto>> LowStockAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InventoryRowDto?> SetStockAsync(long p, SetStockRequest r, long? u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InventoryRowDto?> AdjustAsync(long p, AdjustStockRequest r, long? u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<InventoryTransactionDto>> TransactionsAsync(long p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<VariantInventoryDto>> VariantInventoryAsync(long p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<VariantInventoryDto?> SetVariantStockAsync(long p, long v, SetStockRequest r, long? u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ReserveAsync(long p, long? v, int q, string? rt, long? ri, CancellationToken ct = default) => Task.FromResult(true);
        public Task ReleaseAsync(long p, long? v, int q, string? rt, long? ri, CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitAsync(long p, long? v, int q, string? rt, long? ri, CancellationToken ct = default) => Task.CompletedTask;
        public Task RestockAsync(long p, long? v, int q, string? rt, long? ri, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubInvoices : IInvoiceService
    {
        public Task<(long invoiceId, string invoiceNumber)> GenerateForOrderAsync(long orderId, CancellationToken ct = default)
            => Task.FromResult((1L, "INV-TEST-1"));
        public Task<InvoicePdf?> RenderPdfAsync(long orderId, long? userId, bool isAdmin, CancellationToken ct = default)
            => Task.FromResult<InvoicePdf?>(null);
    }

    private sealed class NoShiprocket : ITenantShiprocketService
    {
        public Task<bool> IsEnabledAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task<ShiprocketRate?> GetCheapestRateAsync(string deliveryPincode, decimal weightKg, bool cod, CancellationToken ct = default) => Task.FromResult<ShiprocketRate?>(null);
        public Task<ShiprocketShipResult> ShipAsync(ShiprocketOrderInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> SchedulePickupAsync(string providerShipmentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GenerateLabelAsync(string providerShipmentId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
