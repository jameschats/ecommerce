using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Inventory;

public static class InventoryTxnType
{
    public const string Purchase = "Purchase";
    public const string Sale = "Sale";
    public const string Reservation = "Reservation";
    public const string Release = "Release";
    public const string Adjustment = "Adjustment";
    public const string Return = "Return";
}

public interface IInventoryService
{
    Task<PagedResult<InventoryRowDto>> ListAsync(InventoryQuery query, CancellationToken ct = default);
    Task<List<InventoryRowDto>> LowStockAsync(CancellationToken ct = default);
    Task<InventoryRowDto?> SetStockAsync(long productId, SetStockRequest req, long? userId, CancellationToken ct = default);
    Task<InventoryRowDto?> AdjustAsync(long productId, AdjustStockRequest req, long? userId, CancellationToken ct = default);
    Task<List<InventoryTransactionDto>> TransactionsAsync(long productId, CancellationToken ct = default);
    Task<List<VariantInventoryDto>> VariantInventoryAsync(long productId, CancellationToken ct = default);
    Task<VariantInventoryDto?> SetVariantStockAsync(long productId, long variantId, SetStockRequest req, long? userId, CancellationToken ct = default);

    // Hooks used by Cart/Order (Stage 4/5)
    Task<bool> ReserveAsync(long productId, long? variantId, int qty, string? refType, long? refId, CancellationToken ct = default);
    Task ReleaseAsync(long productId, long? variantId, int qty, string? refType, long? refId, CancellationToken ct = default);
    Task CommitAsync(long productId, long? variantId, int qty, string? refType, long? refId, CancellationToken ct = default);
    Task RestockAsync(long productId, long? variantId, int qty, string? refType, long? refId, CancellationToken ct = default);
}

public sealed class InventoryService : IInventoryService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;
    private readonly INotificationFeedService _feed;
    private readonly ecomm.api.Features.PublicApi.IWebhookDispatchService _webhooks;
    private readonly IBackInStockService _backInStock;
    private readonly ILogger<InventoryService> _log;

    public InventoryService(EcommerceDbContext db, INotificationFeedService feed,
        ecomm.api.Features.PublicApi.IWebhookDispatchService webhooks, IBackInStockService backInStock, ILogger<InventoryService> log)
    {
        _db = db;
        _feed = feed;
        _webhooks = webhooks;
        _backInStock = backInStock;
        _log = log;
    }

    // Fired only from the merchant-driven "set/adjust stock" entry points below, not from the
    // high-frequency internal Reserve/Release/Commit/Restock hooks used by every cart/order action —
    // those are already surfaced via order.created/order.updated and would otherwise flood integrators.
    private async Task DispatchWebhookAsync(string eventType, object payload, CancellationToken ct)
    {
        try { await _webhooks.DispatchAsync(eventType, payload, ct); }
        catch (Exception ex) { _log.LogError(ex, "Webhook dispatch '{Event}' failed.", eventType); }
    }

    public async Task<PagedResult<InventoryRowDto>> ListAsync(InventoryQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var products = _db.Products.Where(p => p.TenantId == Tenant && !p.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            products = products.Where(p => p.Name.Contains(s) || p.Sku.Contains(s));
        }
        if (query.LowStockOnly)
        {
            products = products.Where(p => _db.Inventory.Any(
                i => i.ProductId == p.ProductId && i.ProductVariantId == null && i.AvailableQty <= i.ReorderLevel));
        }

        products = products.OrderBy(p => p.Name);
        var total = await products.LongCountAsync(ct);

        // Project last, with scalar correlated subqueries (translatable + paginated on the entity).
        var items = await products
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new InventoryRowDto(
                p.ProductId, p.Sku, p.Name,
                _db.Inventory.Where(i => i.ProductId == p.ProductId && i.ProductVariantId == null).Select(i => i.AvailableQty).FirstOrDefault(),
                _db.Inventory.Where(i => i.ProductId == p.ProductId && i.ProductVariantId == null).Select(i => i.ReservedQty).FirstOrDefault(),
                _db.Inventory.Where(i => i.ProductId == p.ProductId && i.ProductVariantId == null).Select(i => i.ReorderLevel).FirstOrDefault(),
                _db.Inventory.Any(i => i.ProductId == p.ProductId && i.ProductVariantId == null && i.AvailableQty <= i.ReorderLevel),
                _db.ProductVariants.Any(v => v.ProductId == p.ProductId)))
            .ToListAsync(ct);

        return new PagedResult<InventoryRowDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public Task<List<InventoryRowDto>> LowStockAsync(CancellationToken ct = default) =>
        (from p in _db.Products
         where p.TenantId == Tenant && !p.IsDeleted
         from i in _db.Inventory.Where(i => i.ProductId == p.ProductId && i.ProductVariantId == null)
         where i.AvailableQty <= i.ReorderLevel
         orderby i.AvailableQty
         select new InventoryRowDto(p.ProductId, p.Sku, p.Name, i.AvailableQty, i.ReservedQty, i.ReorderLevel, true,
             _db.ProductVariants.Any(v => v.ProductId == p.ProductId)))
        .ToListAsync(ct);

    public async Task<InventoryRowDto?> SetStockAsync(long productId, SetStockRequest req, long? userId, CancellationToken ct = default)
    {
        if (!await ProductExists(productId, ct)) return null;
        Validate(req);
        var inv = await SetStockInternalAsync(productId, null, req, userId, ct);
        await DispatchWebhookAsync("inventory.updated", new
        {
            productId, variantId = (long?)null, availableQty = inv.AvailableQty, reorderLevel = inv.ReorderLevel,
        }, ct);
        return ToRow(productId, inv);
    }

    public async Task<VariantInventoryDto?> SetVariantStockAsync(long productId, long variantId, SetStockRequest req, long? userId, CancellationToken ct = default)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.ProductVariantId == variantId && v.ProductId == productId, ct);
        if (variant is null) return null;
        Validate(req);
        var inv = await SetStockInternalAsync(productId, variantId, req, userId, ct);
        await DispatchWebhookAsync("inventory.updated", new
        {
            productId, variantId = (long?)variantId, availableQty = inv.AvailableQty, reorderLevel = inv.ReorderLevel,
        }, ct);
        return new VariantInventoryDto(variantId, variant.Sku, variant.Name, inv.AvailableQty, inv.ReservedQty, inv.ReorderLevel, inv.AvailableQty <= inv.ReorderLevel);
    }

    public Task<List<VariantInventoryDto>> VariantInventoryAsync(long productId, CancellationToken ct = default) =>
        _db.ProductVariants.Where(v => v.ProductId == productId).OrderBy(v => v.ProductVariantId)
            .Select(v => new
            {
                v.ProductVariantId, v.Sku, v.Name,
                Inv = _db.Inventory.FirstOrDefault(i => i.ProductId == productId && i.ProductVariantId == v.ProductVariantId),
            })
            .Select(x => new VariantInventoryDto(
                x.ProductVariantId, x.Sku, x.Name,
                x.Inv != null ? x.Inv.AvailableQty : 0,
                x.Inv != null ? x.Inv.ReservedQty : 0,
                x.Inv != null ? x.Inv.ReorderLevel : 0,
                x.Inv != null && x.Inv.AvailableQty <= x.Inv.ReorderLevel))
            .ToListAsync(ct);

    private async Task<Data.Entities.Inventory> SetStockInternalAsync(long productId, long? variantId, SetStockRequest req, long? userId, CancellationToken ct)
    {
        var inv = await GetOrCreateAsync(productId, variantId, ct);
        var before = inv.AvailableQty;
        var delta = req.AvailableQty - inv.AvailableQty;
        inv.AvailableQty = req.AvailableQty;
        inv.ReorderLevel = req.ReorderLevel;
        inv.UpdatedAt = DateTime.UtcNow;
        if (delta != 0)
            AddTransaction(inv, delta, InventoryTxnType.Adjustment, "Manual", null, "Stock set by admin", userId);
        await _db.SaveChangesAsync(ct);
        // Product came back in stock → email the shoppers waiting on it (product-level stock only).
        if (variantId is null && before <= 0 && inv.AvailableQty > 0)
            await _backInStock.NotifyRestockAsync(productId, ct);
        return inv;
    }

    private static void Validate(SetStockRequest req)
    {
        if (req.AvailableQty < 0 || req.ReorderLevel < 0) throw new AppException("Quantities cannot be negative.");
    }

    public async Task<InventoryRowDto?> AdjustAsync(long productId, AdjustStockRequest req, long? userId, CancellationToken ct = default)
    {
        if (!await ProductExists(productId, ct)) return null;
        var inv = await GetOrCreateAsync(productId, null, ct);
        var before = inv.AvailableQty;
        inv.AvailableQty = Math.Max(0, inv.AvailableQty + req.ChangeQty);
        inv.UpdatedAt = DateTime.UtcNow;
        AddTransaction(inv, req.ChangeQty, InventoryTxnType.Adjustment, "Manual", null, req.Notes, userId);
        await _db.SaveChangesAsync(ct);
        await DispatchWebhookAsync("inventory.updated", new
        {
            productId, variantId = (long?)null, availableQty = inv.AvailableQty, reorderLevel = inv.ReorderLevel,
        }, ct);
        if (before <= 0 && inv.AvailableQty > 0)
            await _backInStock.NotifyRestockAsync(productId, ct);
        return ToRow(productId, inv);
    }

    public Task<List<InventoryTransactionDto>> TransactionsAsync(long productId, CancellationToken ct = default) =>
        _db.InventoryTransactions.Where(t => t.ProductId == productId)
            .OrderByDescending(t => t.InventoryTransactionId)
            .Select(t => new InventoryTransactionDto(t.InventoryTransactionId, t.ChangeQty, t.BalanceAfter, t.TransactionType, t.Notes, t.CreatedAt))
            .Take(100)
            .ToListAsync(ct);

    public async Task<bool> ReserveAsync(long productId, long? variantId, int qty, string? refType, long? refId, CancellationToken ct = default)
    {
        if (qty <= 0) return true;
        var inv = await GetOrCreateAsync(productId, variantId, ct);
        if (inv.AvailableQty < qty) return false;
        var before = inv.AvailableQty;
        inv.AvailableQty -= qty;
        inv.ReservedQty += qty;
        inv.UpdatedAt = DateTime.UtcNow;
        AddTransaction(inv, -qty, InventoryTxnType.Reservation, refType, refId, null, null);
        await _db.SaveChangesAsync(ct);

        // Notify admins once, when stock first drops to/below the reorder level.
        if (inv.ReorderLevel > 0 && before > inv.ReorderLevel && inv.AvailableQty <= inv.ReorderLevel)
        {
            var name = await _db.Products.Where(p => p.ProductId == productId).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? "A product";
            await _feed.NotifyAdminsAsync("LowStock", $"Low stock: {name}",
                $"{inv.AvailableQty} left (reorder at {inv.ReorderLevel})", "/admin/inventory", ct);
        }
        return true;
    }

    public async Task ReleaseAsync(long productId, long? variantId, int qty, string? refType, long? refId, CancellationToken ct = default)
    {
        if (qty <= 0) return;
        var inv = await GetOrCreateAsync(productId, variantId, ct);
        var released = Math.Min(qty, inv.ReservedQty);
        inv.ReservedQty -= released;
        inv.AvailableQty += released;
        inv.UpdatedAt = DateTime.UtcNow;
        AddTransaction(inv, released, InventoryTxnType.Release, refType, refId, null, null);
        await _db.SaveChangesAsync(ct);
    }

    public async Task CommitAsync(long productId, long? variantId, int qty, string? refType, long? refId, CancellationToken ct = default)
    {
        if (qty <= 0) return;
        var inv = await GetOrCreateAsync(productId, variantId, ct);
        inv.ReservedQty = Math.Max(0, inv.ReservedQty - qty);
        inv.UpdatedAt = DateTime.UtcNow;
        AddTransaction(inv, -qty, InventoryTxnType.Sale, refType, refId, null, null);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Return committed (sold) stock to available — used when a paid order is cancelled.</summary>
    public async Task RestockAsync(long productId, long? variantId, int qty, string? refType, long? refId, CancellationToken ct = default)
    {
        if (qty <= 0) return;
        var inv = await GetOrCreateAsync(productId, variantId, ct);
        inv.AvailableQty += qty;
        inv.UpdatedAt = DateTime.UtcNow;
        AddTransaction(inv, qty, InventoryTxnType.Return, refType, refId, "Order cancelled", null);
        await _db.SaveChangesAsync(ct);
    }

    // --- helpers ---

    private Task<bool> ProductExists(long productId, CancellationToken ct) =>
        _db.Products.AnyAsync(p => p.ProductId == productId && p.TenantId == Tenant && !p.IsDeleted, ct);

    private async Task<Data.Entities.Inventory> GetOrCreateAsync(long productId, long? variantId, CancellationToken ct)
    {
        var inv = await _db.Inventory.FirstOrDefaultAsync(
            i => i.ProductId == productId && i.ProductVariantId == variantId, ct);
        if (inv is null)
        {
            inv = new Data.Entities.Inventory { TenantId = Tenant, ProductId = productId, ProductVariantId = variantId, CreatedAt = DateTime.UtcNow };
            _db.Inventory.Add(inv);
            await _db.SaveChangesAsync(ct);  // persist so the row has an Id for transactions
        }
        return inv;
    }

    private void AddTransaction(Data.Entities.Inventory inv, int changeQty, string type, string? refType, long? refId, string? notes, long? userId) =>
        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryId = inv.InventoryId,
            ProductId = inv.ProductId,
            ChangeQty = changeQty,
            BalanceAfter = inv.AvailableQty,
            TransactionType = type,
            ReferenceType = refType,
            ReferenceId = refId,
            Notes = notes,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
        });

    private static InventoryRowDto ToRow(long productId, Data.Entities.Inventory inv) =>
        new(productId, string.Empty, string.Empty, inv.AvailableQty, inv.ReservedQty, inv.ReorderLevel,
            inv.AvailableQty <= inv.ReorderLevel, false);
}
