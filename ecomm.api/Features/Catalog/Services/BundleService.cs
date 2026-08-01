using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

public sealed record BundleItemInput(long ComponentProductId, long? ComponentVariantId, int Quantity);

public sealed record BundleComponentDto(
    long ComponentProductId, long? ComponentVariantId, string Name, string Slug,
    string? VariantLabel, string? ImageUrl, int Quantity, int AvailableQty);

public interface IBundleService
{
    Task<List<BundleComponentDto>> ComponentsAsync(long bundleProductId, CancellationToken ct = default);
    /// <summary>How many bundles can be made right now — the tightest component, floor(available / needed).
    /// Zero (never purchasable) when the bundle has no components configured.</summary>
    Task<int> AvailableQtyAsync(long bundleProductId, CancellationToken ct = default);
    Task<List<BundleComponentDto>> SaveComponentsAsync(long bundleProductId, IReadOnlyList<BundleItemInput> items, CancellationToken ct = default);
    /// <summary>The real inventory lines an order line for <paramref name="productId"/> needs: the bundle's
    /// components (each Quantity scaled by how many bundles were bought), or just the line itself when it
    /// isn't a bundle at all — so every checkout call site can treat both uniformly.</summary>
    Task<List<(long ProductId, long? VariantId, int Quantity)>> ExpandForInventoryAsync(long productId, long? variantId, int quantity, CancellationToken ct = default);
}

public sealed class BundleService : IBundleService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;
    public BundleService(EcommerceDbContext db) => _db = db;

    public async Task<List<BundleComponentDto>> ComponentsAsync(long bundleProductId, CancellationToken ct = default)
    {
        var rows = await (from bi in _db.BundleItems
                           where bi.BundleProductId == bundleProductId && bi.TenantId == Tenant
                           join p in _db.Products on bi.ComponentProductId equals p.ProductId
                           orderby bi.BundleItemId
                           select new
                           {
                               bi.ComponentProductId,
                               bi.ComponentVariantId,
                               bi.Quantity,
                               p.Name,
                               p.Slug,
                               ImageUrl = _db.ProductImages.Where(im => im.ProductId == bi.ComponentProductId)
                                   .OrderByDescending(im => im.IsPrimary).ThenBy(im => im.DisplayOrder).Select(im => im.Url).FirstOrDefault(),
                               VariantLabel = bi.ComponentVariantId == null ? null :
                                   _db.ProductVariants.Where(v => v.ProductVariantId == bi.ComponentVariantId).Select(v => v.Name).FirstOrDefault(),
                               Available = _db.Inventory.Where(i => i.ProductId == bi.ComponentProductId).Select(i => (int?)i.AvailableQty).Sum() ?? 0,
                           }).ToListAsync(ct);

        return rows.Select(r => new BundleComponentDto(r.ComponentProductId, r.ComponentVariantId, r.Name, r.Slug, r.VariantLabel, r.ImageUrl, r.Quantity, r.Available)).ToList();
    }

    public async Task<int> AvailableQtyAsync(long bundleProductId, CancellationToken ct = default)
    {
        var components = await ComponentsAsync(bundleProductId, ct);
        return components.Count == 0 ? 0 : components.Min(c => c.Quantity > 0 ? c.AvailableQty / c.Quantity : 0);
    }

    public async Task<List<BundleComponentDto>> SaveComponentsAsync(long bundleProductId, IReadOnlyList<BundleItemInput> items, CancellationToken ct = default)
    {
        var bundle = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == bundleProductId && p.TenantId == Tenant && !p.IsDeleted, ct)
            ?? throw new AppException("Product not found.", 404);
        if (!bundle.IsBundle) throw new AppException("This product isn't marked as a bundle.");

        // Empty is allowed (clears the bundle — it's just not purchasable until re-populated; enforced by
        // AvailableQtyAsync returning 0 for no components, not by rejecting the save here).
        var clean = items.Where(i => i.Quantity > 0 && i.ComponentProductId != bundleProductId).ToList();

        // No bundle-of-bundles — keeps inventory expansion a single flat pass, no recursion/cycles to guard against.
        var ids = clean.Select(i => i.ComponentProductId).Distinct().ToList();
        var validIds = await _db.Products.Where(p => ids.Contains(p.ProductId) && p.TenantId == Tenant && !p.IsDeleted && !p.IsBundle)
            .Select(p => p.ProductId).ToListAsync(ct);
        if (ids.Except(validIds).Any()) throw new AppException("One or more selected products don't exist.");

        _db.BundleItems.RemoveRange(await _db.BundleItems.Where(b => b.BundleProductId == bundleProductId && b.TenantId == Tenant).ToListAsync(ct));
        foreach (var i in clean)
            _db.BundleItems.Add(new BundleItem
            {
                TenantId = Tenant, BundleProductId = bundleProductId,
                ComponentProductId = i.ComponentProductId, ComponentVariantId = i.ComponentVariantId, Quantity = i.Quantity,
            });
        await _db.SaveChangesAsync(ct);
        return await ComponentsAsync(bundleProductId, ct);
    }

    public async Task<List<(long ProductId, long? VariantId, int Quantity)>> ExpandForInventoryAsync(long productId, long? variantId, int quantity, CancellationToken ct = default)
    {
        var items = await _db.BundleItems.Where(b => b.BundleProductId == productId && b.TenantId == Tenant).ToListAsync(ct);
        return items.Count == 0
            ? [(productId, variantId, quantity)]
            : items.Select(i => (i.ComponentProductId, i.ComponentVariantId, i.Quantity * quantity)).ToList();
    }
}
