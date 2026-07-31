using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Wishlist;

public interface IWishlistService
{
    Task<List<ProductListItemDto>> GetAsync(long userId, CancellationToken ct = default);
    Task<List<long>> GetProductIdsAsync(long userId, CancellationToken ct = default);
    Task AddAsync(long userId, long productId, CancellationToken ct = default);
    Task RemoveAsync(long userId, long productId, CancellationToken ct = default);
}

public sealed class WishlistService : IWishlistService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;

    public WishlistService(EcommerceDbContext db) => _db = db;

    public Task<List<ProductListItemDto>> GetAsync(long userId, CancellationToken ct = default) =>
        _db.WishlistItems.AsNoTracking()
            .Where(w => w.TenantId == Tenant && w.UserId == userId)
            .OrderByDescending(w => w.WishlistItemId)
            .Join(_db.Products.Where(p => !p.IsDeleted && p.IsActive), w => w.ProductId, p => p.ProductId, (w, p) => p)
            .Select(p => new ProductListItemDto(
                p.ProductId, p.Sku, p.Name, p.Slug, p.Price, p.CompareAtPrice, p.Status, p.IsFeatured,
                p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                p.Category!.Name,
                p.Brand != null ? p.Brand.Name : null,
                p.InventoryRecords.Sum(i => i.AvailableQty) > 0,
                p.InventoryRecords.Sum(i => i.AvailableQty),
                p.InventoryRecords.Any(i => i.ReorderLevel > 0 && i.AvailableQty <= i.ReorderLevel),
                p.Variants.SelectMany(v => v.Options).Where(o => o.OptionName == "Color").Select(o => o.OptionValue).Distinct().ToList(),
                p.CreatedAt,
                p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.Url).Skip(1).FirstOrDefault()))
            .ToListAsync(ct);

    public Task<List<long>> GetProductIdsAsync(long userId, CancellationToken ct = default) =>
        _db.WishlistItems.AsNoTracking()
            .Where(w => w.TenantId == Tenant && w.UserId == userId)
            .Select(w => w.ProductId).ToListAsync(ct);

    public async Task AddAsync(long userId, long productId, CancellationToken ct = default)
    {
        var exists = await _db.Products.AnyAsync(p => p.ProductId == productId && p.TenantId == Tenant && !p.IsDeleted, ct);
        if (!exists) throw new AppException("Product not found.", 404);
        if (await _db.WishlistItems.AnyAsync(w => w.TenantId == Tenant && w.UserId == userId && w.ProductId == productId, ct))
            return; // idempotent
        _db.WishlistItems.Add(new WishlistItem { TenantId = Tenant, UserId = userId, ProductId = productId, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(long userId, long productId, CancellationToken ct = default)
    {
        var item = await _db.WishlistItems.FirstOrDefaultAsync(
            w => w.TenantId == Tenant && w.UserId == userId && w.ProductId == productId, ct);
        if (item is null) return;
        _db.WishlistItems.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}
