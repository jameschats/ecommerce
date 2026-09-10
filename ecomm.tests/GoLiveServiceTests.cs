using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.GoLive;
using ecomm.api.Features.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class GoLiveServiceTests
{
    private const string Phrase = "DELETE ALL TRANSACTIONS";

    /// <summary>Records every Release/Restock call instead of touching real Inventory rows — GoLiveService's
    /// job is to call the right one with the right quantity per order status, not to reimplement
    /// InventoryService's own balance math (that's already OrderService.CancelOrderAsync's job, mirrored here).</summary>
    private sealed class RecordingInventory : IInventoryService
    {
        public List<(string Action, long ProductId, long? VariantId, int Qty)> Calls { get; } = new();
        public Task<PagedResult<InventoryRowDto>> ListAsync(InventoryQuery q, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<InventoryRowDto>> LowStockAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InventoryRowDto?> SetStockAsync(long p, SetStockRequest r, long? u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InventoryRowDto?> AdjustAsync(long p, AdjustStockRequest r, long? u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<InventoryTransactionDto>> TransactionsAsync(long p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<VariantInventoryDto>> VariantInventoryAsync(long p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<VariantInventoryDto?> SetVariantStockAsync(long p, long v, SetStockRequest r, long? u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ReserveAsync(long p, long? v, int q, string? rt, long? ri, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ReleaseAsync(long p, long? v, int q, string? rt, long? ri, CancellationToken ct = default) { Calls.Add(("Release", p, v, q)); return Task.CompletedTask; }
        public Task CommitAsync(long p, long? v, int q, string? rt, long? ri, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RestockAsync(long p, long? v, int q, string? rt, long? ri, CancellationToken ct = default) { Calls.Add(("Restock", p, v, q)); return Task.CompletedTask; }
    }

    private static (EcommerceDbContext db, GoLiveService svc, RecordingInventory inv) New(long tenantId = 1)
    {
        var db = TestDb.New(tenantId);
        var inv = new RecordingInventory();
        var svc = new GoLiveService(db, inv, new ecomm.api.Features.Catalog.Services.BundleService(db));
        return (db, svc, inv);
    }

    private static Order NewOrder(long tenantId, long id, long userId, string status, decimal total = 100m) => new()
    {
        OrderId = id, TenantId = tenantId, UserId = userId, OrderNumber = $"ORD{id}", Status = status,
        TotalAmount = total, CreatedAt = DateTime.UtcNow,
    };

    private static void SeedTenant(EcommerceDbContext db, long tenantId = 1) =>
        db.Tenants.Add(new Tenant { TenantId = tenantId, Name = "T", Code = "T", NextOrderSeq = 7, NextInvoiceSeq = 20, CreatedAt = DateTime.UtcNow });

    [Fact]
    public async Task Reset_releases_uncommitted_and_restocks_committed_but_skips_cancelled_and_draft()
    {
        var (db, svc, inv) = New();
        SeedTenant(db);
        db.Users.Add(new User { UserId = 5, TenantId = 1, Email = "c@x.com", FullName = "C", CreatedAt = DateTime.UtcNow });
        db.Orders.AddRange(
            NewOrder(1, 1, 5, "Pending"), NewOrder(1, 2, 5, "Paid"),
            NewOrder(1, 3, 5, "Cancelled"), NewOrder(1, 4, 5, "Draft"));
        db.OrderItems.AddRange(
            new OrderItem { OrderId = 1, ProductId = 100, Quantity = 2, ProductName = "P", UnitPrice = 10, LineTotal = 20, CreatedAt = DateTime.UtcNow },
            new OrderItem { OrderId = 2, ProductId = 101, Quantity = 3, ProductName = "Q", UnitPrice = 10, LineTotal = 30, CreatedAt = DateTime.UtcNow },
            new OrderItem { OrderId = 3, ProductId = 102, Quantity = 1, ProductName = "R", UnitPrice = 10, LineTotal = 10, CreatedAt = DateTime.UtcNow },
            new OrderItem { OrderId = 4, ProductId = 103, Quantity = 1, ProductName = "S", UnitPrice = 10, LineTotal = 10, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await svc.ResetAsync(new GoLiveResetRequest(false, false, false, false, Phrase), currentUserId: null);

        Assert.Contains(inv.Calls, c => c.Action == "Release" && c.ProductId == 100 && c.Qty == 2);
        Assert.Contains(inv.Calls, c => c.Action == "Restock" && c.ProductId == 101 && c.Qty == 3);
        Assert.DoesNotContain(inv.Calls, c => c.ProductId == 102);   // Cancelled — already corrected, must not double-count
        Assert.DoesNotContain(inv.Calls, c => c.ProductId == 103);   // Draft — never held stock
        Assert.Equal(2, inv.Calls.Count);
    }

    [Fact]
    public async Task Reset_removes_every_always_removed_table_but_leaves_catalogue_settings_and_other_tenants()
    {
        var dbName = Guid.NewGuid().ToString();

        // Seed a second tenant's own order first, via its own tenant-scoped context — TestDb's
        // ambient-tenant stamping would otherwise overwrite an explicit TenantId=2 on a context fixed
        // to tenant 1 (EcommerceDbContext.StampTenant always stamps the CURRENT tenant on insert).
        using (var otherTenantDb = TestDb.ForDatabase(dbName, tenantId: 2))
        {
            otherTenantDb.Tenants.Add(new Tenant { TenantId = 2, Name = "T2", Code = "T2", CreatedAt = DateTime.UtcNow });
            otherTenantDb.Orders.Add(new Order { OrderId = 2, UserId = 99, OrderNumber = "ORD2", Status = "Paid", TotalAmount = 5, CreatedAt = DateTime.UtcNow });
            await otherTenantDb.SaveChangesAsync();
        }

        var db = TestDb.ForDatabase(dbName, tenantId: 1);
        var inv = new RecordingInventory();
        var svc = new GoLiveService(db, inv, new ecomm.api.Features.Catalog.Services.BundleService(db));
        SeedTenant(db);
        db.Users.Add(new User { UserId = 5, TenantId = 1, Email = "c@x.com", FullName = "C", CreatedAt = DateTime.UtcNow });
        var order = NewOrder(1, 1, 5, "Paid");
        db.Orders.Add(order);
        db.OrderItems.Add(new OrderItem { OrderId = 1, ProductId = 100, Quantity = 1, ProductName = "P", UnitPrice = 10, LineTotal = 10, CreatedAt = DateTime.UtcNow });
        db.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = 1, FromStatus = "Pending", ToStatus = "Paid", CreatedAt = DateTime.UtcNow });
        db.Payments.Add(new Payment { TenantId = 1, OrderId = 1, Amount = 10, CreatedAt = DateTime.UtcNow });
        db.Shipments.Add(new Shipment { TenantId = 1, OrderId = 1, CreatedAt = DateTime.UtcNow });
        db.Invoices.Add(new Invoice { TenantId = 1, OrderId = 1, InvoiceNumber = "INV-1", InvoiceDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        db.Reviews.Add(new Review { TenantId = 1, ProductId = 100, UserId = 5, OrderId = 1, Rating = 5, CreatedAt = DateTime.UtcNow });
        db.CreditNotes.Add(new CreditNote { TenantId = 1, OrderId = 1, CreditNoteNumber = "CN-1", CreatedAt = DateTime.UtcNow });
        var inventory = new Inventory { TenantId = 1, ProductId = 100, AvailableQty = 5, CreatedAt = DateTime.UtcNow };
        db.Inventory.Add(inventory);
        await db.SaveChangesAsync();
        db.InventoryTransactions.Add(new InventoryTransaction { InventoryId = inventory.InventoryId, ProductId = 100, ChangeQty = 5, TransactionType = "Purchase", CreatedAt = DateTime.UtcNow });
        var cart = new Cart { TenantId = 1, UserId = 5, CreatedAt = DateTime.UtcNow };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
        db.CartItems.Add(new CartItem { CartId = cart.CartId, ProductId = 100, UnitPrice = 10, CreatedAt = DateTime.UtcNow });
        db.WishlistItems.Add(new WishlistItem { TenantId = 1, UserId = 5, ProductId = 100, CreatedAt = DateTime.UtcNow });
        db.Notifications.Add(new Notification { TenantId = 1, UserId = 5, Type = "order", Title = "T", CreatedAt = DateTime.UtcNow });
        db.NotificationHistory.Add(new NotificationHistory { TenantId = 1, Recipient = "c@x.com", CreatedAt = DateTime.UtcNow });
        db.OtpVerifications.Add(new OtpVerification { TenantId = 1, Identifier = "c@x.com", Channel = "Email", Purpose = "Login", CodeHash = "h", ExpiresAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        db.RefreshTokens.Add(new RefreshToken { UserId = 5, TokenHash = "h", ExpiresAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        db.UserTwoFactorBackupCodes.Add(new UserTwoFactorBackupCode { UserId = 5, CodeHash = "h", CreatedAt = DateTime.UtcNow });

        // Catalogue/settings that must survive.
        var category = new Category { TenantId = 1, Name = "Cat", Slug = "cat", CreatedAt = DateTime.UtcNow };
        db.Categories.Add(category);
        db.Products.Add(new Product { ProductId = 100, TenantId = 1, CategoryId = 0, Sku = "P1", Name = "Prod", Slug = "prod", Price = 10, CreatedAt = DateTime.UtcNow });
        db.Settings.Add(new Setting { TenantId = 1, SettingKey = "SiteName", SettingValue = "My Store", DataType = "string", Category = "General", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await svc.ResetAsync(new GoLiveResetRequest(false, false, false, false, Phrase), currentUserId: null);

        Assert.Equal(0, await db.Orders.CountAsync());       // tenant-1 orders gone (query filter scopes to tenant 1)
        Assert.Equal(0, await db.OrderItems.CountAsync(i => i.OrderId == 1));
        Assert.Equal(0, await db.OrderStatusHistories.CountAsync(h => h.OrderId == 1));
        Assert.Equal(0, await db.Payments.CountAsync());
        Assert.Equal(0, await db.Shipments.CountAsync());
        Assert.Equal(0, await db.Invoices.CountAsync());
        Assert.Equal(0, await db.Reviews.CountAsync());
        Assert.Equal(0, await db.CreditNotes.CountAsync());
        Assert.Equal(0, await db.InventoryTransactions.CountAsync(t => t.InventoryId == inventory.InventoryId));
        Assert.Equal(0, await db.Carts.CountAsync());
        Assert.Equal(0, await db.CartItems.CountAsync(i => i.CartId == cart.CartId));
        Assert.Equal(0, await db.WishlistItems.CountAsync());
        Assert.Equal(0, await db.Notifications.CountAsync());
        Assert.Equal(0, await db.NotificationHistory.CountAsync());
        Assert.Equal(0, await db.OtpVerifications.CountAsync());
        Assert.Equal(0, await db.RefreshTokens.CountAsync(t => t.UserId == 5));
        Assert.Equal(0, await db.UserTwoFactorBackupCodes.CountAsync(c => c.UserId == 5));

        // Catalogue/settings survive.
        Assert.Equal(1, await db.Categories.CountAsync());
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal(1, await db.Settings.CountAsync());
        Assert.Equal(5, (await db.Inventory.FirstAsync()).AvailableQty);   // balance itself untouched by step 5

        // Numbering reset.
        var tenant = await db.Tenants.SingleAsync(t => t.TenantId == 1);
        Assert.Equal(1, tenant.NextOrderSeq);
        Assert.Equal(1, tenant.NextInvoiceSeq);

        // Tenant 2's order is completely untouched — read back through its own tenant-scoped context.
        using var asTenant2 = TestDb.ForDatabase(dbName, tenantId: 2);
        Assert.Equal(1, await asTenant2.Orders.CountAsync());
    }

    [Fact]
    public async Task Reset_only_removes_optional_categories_when_requested()
    {
        var (db, svc, _) = New();
        SeedTenant(db);
        db.Users.Add(new User { UserId = 1, TenantId = 1, Email = "admin@x.com", FullName = "Admin", CreatedAt = DateTime.UtcNow });
        db.Roles.Add(new Role { RoleId = 1, TenantId = 1, Name = "Customer", NormalizedName = "CUSTOMER", CreatedAt = DateTime.UtcNow });
        var customer = new User { UserId = 5, TenantId = 1, Email = "c@x.com", FullName = "C", CreatedAt = DateTime.UtcNow };
        db.Users.Add(customer);
        db.CustomerEvents.Add(new CustomerEvent { TenantId = 1, SessionId = "s1", EventType = "view", CreatedAt = DateTime.UtcNow });
        db.ImportJobs.Add(new ImportJob { TenantId = 1, JobType = "Products", CreatedAt = DateTime.UtcNow });
        db.ContactMessages.Add(new ContactMessage { TenantId = 1, Name = "N", Email = "n@x.com", Body = "hi", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = customer.UserId, RoleId = 1 });
        await db.SaveChangesAsync();

        // First: no opt-ins — nothing optional is removed.
        await svc.ResetAsync(new GoLiveResetRequest(false, false, false, false, Phrase), currentUserId: 1);
        Assert.Equal(1, await db.Users.CountAsync(u => u.UserId == customer.UserId));
        Assert.Equal(1, await db.CustomerEvents.CountAsync());
        Assert.Equal(1, await db.ImportJobs.CountAsync());
        Assert.Equal(1, await db.ContactMessages.CountAsync());

        // Reopen the store (fresh reset target) and opt into everything.
        (await db.Tenants.SingleAsync(t => t.TenantId == 1)).GoneLiveAt = null;
        var result = await svc.ResetAsync(new GoLiveResetRequest(true, true, true, true, Phrase), currentUserId: 1);

        Assert.Equal(0, await db.Users.CountAsync(u => u.UserId == customer.UserId));
        Assert.Equal(1, await db.Users.CountAsync(u => u.UserId == 1));   // signed-in admin always kept
        Assert.Equal(0, await db.CustomerEvents.CountAsync());
        Assert.Equal(0, await db.ImportJobs.CountAsync());
        Assert.Equal(0, await db.ContactMessages.CountAsync());
        Assert.Equal(1, result.CustomersRemoved);
    }

    [Fact]
    public async Task Reset_rejects_wrong_confirmation_text()
    {
        var (db, svc, _) = New();
        SeedTenant(db);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppException>(() =>
            svc.ResetAsync(new GoLiveResetRequest(false, false, false, false, "delete all transactions"), null));
    }

    [Fact]
    public async Task MarkLive_gates_reset_and_itself_permanently()
    {
        var (db, svc, _) = New();
        SeedTenant(db);
        await db.SaveChangesAsync();

        await svc.MarkLiveAsync();

        var ex1 = await Assert.ThrowsAsync<AppException>(() => svc.MarkLiveAsync());
        Assert.Equal(409, ex1.StatusCode);
        var ex2 = await Assert.ThrowsAsync<AppException>(() =>
            svc.ResetAsync(new GoLiveResetRequest(false, false, false, false, Phrase), null));
        Assert.Equal(409, ex2.StatusCode);
    }

    [Fact]
    public async Task Summary_reports_reserved_units_and_next_invoice_preview()
    {
        var (db, svc, _) = New();
        SeedTenant(db);
        db.Inventory.Add(new Inventory { TenantId = 1, ProductId = 100, AvailableQty = 3, ReservedQty = 2, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var summary = await svc.GetSummaryAsync();

        Assert.False(summary.IsLive);
        Assert.Equal(2, summary.ReservedUnits);
        Assert.Equal(1, summary.ProductsWithReservedUnits);
        Assert.Equal($"INV-{DateTime.UtcNow:yyyy}-00020", summary.NextInvoicePreview);
    }
}
