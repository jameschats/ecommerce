using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

public interface IVariantService
{
    Task<List<ProductVariantDto>> ListAsync(long productId, CancellationToken ct = default);
    Task<ProductVariantDto?> CreateAsync(long productId, SaveVariantRequest req, CancellationToken ct = default);
    Task<ProductVariantDto?> UpdateAsync(long productId, long variantId, SaveVariantRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(long productId, long variantId, CancellationToken ct = default);
}

public sealed class VariantService : IVariantService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;

    public VariantService(EcommerceDbContext db) => _db = db;

    public Task<List<ProductVariantDto>> ListAsync(long productId, CancellationToken ct = default) =>
        _db.ProductVariants
            .Where(v => v.ProductId == productId)
            .OrderBy(v => v.ProductVariantId)
            .Select(v => new ProductVariantDto(v.ProductVariantId, v.Sku, v.Name, v.PriceAdjustment, v.IsActive,
                v.Options.Select(o => new VariantOptionDto(o.OptionName, o.OptionValue)).ToList()))
            .ToListAsync(ct);

    public async Task<ProductVariantDto?> CreateAsync(long productId, SaveVariantRequest req, CancellationToken ct = default)
    {
        if (!await ProductExists(productId, ct)) return null;
        await EnsureSkuFree(req.Sku, null, ct);

        var now = DateTime.UtcNow;
        var variant = new ProductVariant
        {
            ProductId = productId,
            Sku = req.Sku.Trim(),
            Name = req.Name,
            PriceAdjustment = req.PriceAdjustment,
            IsActive = req.IsActive,
            CreatedAt = now,
            Options = BuildOptions(req.Options, now),
        };
        _db.ProductVariants.Add(variant);
        await _db.SaveChangesAsync(ct);
        return Map(variant);
    }

    public async Task<ProductVariantDto?> UpdateAsync(long productId, long variantId, SaveVariantRequest req, CancellationToken ct = default)
    {
        var variant = await _db.ProductVariants.Include(v => v.Options)
            .FirstOrDefaultAsync(v => v.ProductVariantId == variantId && v.ProductId == productId, ct);
        if (variant is null) return null;
        await EnsureSkuFree(req.Sku, variantId, ct);

        var now = DateTime.UtcNow;
        variant.Sku = req.Sku.Trim();
        variant.Name = req.Name;
        variant.PriceAdjustment = req.PriceAdjustment;
        variant.IsActive = req.IsActive;
        variant.UpdatedAt = now;

        _db.VariantOptions.RemoveRange(variant.Options);
        variant.Options = BuildOptions(req.Options, now);
        await _db.SaveChangesAsync(ct);
        return Map(variant);
    }

    public async Task<bool> DeleteAsync(long productId, long variantId, CancellationToken ct = default)
    {
        var variant = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.ProductVariantId == variantId && v.ProductId == productId, ct);
        if (variant is null) return false;
        _db.ProductVariants.Remove(variant);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private Task<bool> ProductExists(long productId, CancellationToken ct) =>
        _db.Products.AnyAsync(p => p.ProductId == productId && p.TenantId == Tenant && !p.IsDeleted, ct);

    private async Task EnsureSkuFree(string sku, long? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new AppException("Variant SKU is required.");
        var trimmed = sku.Trim();
        if (await _db.ProductVariants.AnyAsync(v => v.Sku == trimmed && v.ProductVariantId != (excludeId ?? 0), ct))
            throw new AppException($"Variant SKU '{trimmed}' already exists.", StatusCodes.Status409Conflict);
    }

    private static List<VariantOption> BuildOptions(IReadOnlyList<VariantOptionDto>? options, DateTime now) =>
        (options ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o.OptionName))
            .Select(o => new VariantOption { OptionName = o.OptionName.Trim(), OptionValue = o.OptionValue, CreatedAt = now })
            .ToList();

    private static ProductVariantDto Map(ProductVariant v) =>
        new(v.ProductVariantId, v.Sku, v.Name, v.PriceAdjustment, v.IsActive,
            v.Options.Select(o => new VariantOptionDto(o.OptionName, o.OptionValue)).ToList());
}
