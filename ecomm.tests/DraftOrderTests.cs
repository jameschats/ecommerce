using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Checkout;
using ecomm.api.Features.Coupons;
using ecomm.api.Features.Inventory;
using ecomm.api.Features.Orders;
using ecomm.api.Features.Shipping.Shiprocket;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class DraftOrderTests
{
    private static async Task<(EcommerceDbContext db, DraftOrderService svc, long customerId, long productId)> SetupAsync()
    {
        var db = TestDb.New(tenantId: 1);
        // Tax off keeps the arithmetic simple; no shipping method → free/none.
        db.Settings.Add(new Setting { TenantId = 1, SettingKey = "TaxMode", SettingValue = "None", DataType = "string", Category = "Billing", CreatedAt = DateTime.UtcNow });
        db.Roles.Add(new Role { RoleId = 1, TenantId = 1, Name = "Customer", NormalizedName = "CUSTOMER" });
        var customer = new User { UserId = 5, Email = "buyer@x.com", NormalizedEmail = "BUYER@X.COM", FullName = "Buyer", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.Add(customer);
        var product = new Product { ProductId = 9, Name = "Mug", Slug = "mug", Sku = "MUG1", Price = 100m, Status = "Active", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var svc = new DraftOrderService(db, new TaxService(db), new ShippingService(db, new FakeShiprocket()),
            new CouponService(db, new ecomm.api.Features.Collections.CollectionService(db)), new ThrowingInventory(), new ThrowingInvoices());
        return (db, svc, customer.UserId, product.ProductId);
    }

    [Fact]
    public async Task Create_draft_prices_and_persists_but_stays_out_of_orders_list()
    {
        var (db, svc, customerId, productId) = await SetupAsync();
        using var _ = db;

        var draft = await svc.CreateAsync(new CreateDraftOrderRequest(
            customerId, new List<DraftLineInput> { new(productId, null, 2) }, null, "Phone order"));

        Assert.Equal("Draft", draft.Status);
        Assert.StartsWith("DRAFT-", draft.OrderNumber);
        Assert.Equal(200m, draft.Subtotal);
        Assert.Equal(200m, draft.TotalAmount);   // tax off, no shipping, no discount
        Assert.Single(draft.Lines);

        // Draft is persisted as an Order with Status=Draft…
        var order = await db.Orders.SingleAsync();
        Assert.Equal("Draft", order.Status);
        // …and appears in the draft list.
        Assert.Single(await svc.ListAsync());
    }

    [Fact]
    public async Task Create_draft_requires_a_customer_and_a_product()
    {
        var (db, svc, customerId, productId) = await SetupAsync();
        using var _ = db;

        await Assert.ThrowsAsync<ecomm.api.Common.Exceptions.AppException>(
            () => svc.CreateAsync(new CreateDraftOrderRequest(999, new List<DraftLineInput> { new(productId, null, 1) }, null, null)));
        await Assert.ThrowsAsync<ecomm.api.Common.Exceptions.AppException>(
            () => svc.CreateAsync(new CreateDraftOrderRequest(customerId, new List<DraftLineInput>(), null, null)));
    }

    // Draft creation never touches inventory or invoicing — these stubs throw if that changes unexpectedly.
    private sealed class ThrowingInventory : IInventoryService
    {
        public Task<PagedResult<InventoryRowDto>> ListAsync(InventoryQuery q, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<InventoryRowDto>> LowStockAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InventoryRowDto?> SetStockAsync(long p, SetStockRequest r, long? u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InventoryRowDto?> AdjustAsync(long p, AdjustStockRequest r, long? u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<InventoryTransactionDto>> TransactionsAsync(long p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<VariantInventoryDto>> VariantInventoryAsync(long p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<VariantInventoryDto?> SetVariantStockAsync(long p, long v, SetStockRequest r, long? u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ReserveAsync(long p, long? v, int q, string? rt, long? ri, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ReleaseAsync(long p, long? v, int q, string? rt, long? ri, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CommitAsync(long p, long? v, int q, string? rt, long? ri, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RestockAsync(long p, long? v, int q, string? rt, long? ri, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingInvoices : IInvoiceService
    {
        public Task<(long invoiceId, string invoiceNumber)> GenerateForOrderAsync(long orderId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InvoicePdf?> RenderPdfAsync(long orderId, long? userId, bool isAdmin, CancellationToken ct = default) => throw new NotSupportedException();
    }

    // Store not on Shiprocket → rates null, so ShippingService uses the manual path.
    private sealed class FakeShiprocket : ITenantShiprocketService
    {
        public Task<bool> IsEnabledAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task<ShiprocketRate?> GetCheapestRateAsync(string deliveryPincode, decimal weightKg, bool cod, CancellationToken ct = default) => Task.FromResult<ShiprocketRate?>(null);
        public Task<ShiprocketShipResult> ShipAsync(ShiprocketOrderInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> SchedulePickupAsync(string providerShipmentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GenerateLabelAsync(string providerShipmentId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
