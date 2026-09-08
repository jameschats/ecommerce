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
    Task<List<ProductVariantDto>?> GenerateAsync(long productId, GenerateVariantsRequest req, CancellationToken ct = default);
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

    public async Task<List<ProductVariantDto>?> GenerateAsync(long productId, GenerateVariantsRequest req, CancellationToken ct = default)
    {
        var product = await _db.Products
            .Where(p => p.ProductId == productId && p.TenantId == Tenant && !p.IsDeleted)
            .Select(p => new { p.Sku })
            .FirstOrDefaultAsync(ct);
        if (product is null) return null;

        var groups = (req.Options ?? [])
            .Select(g => new { Name = g.Name?.Trim() ?? "", Values = (g.Values ?? []).Select(v => v.Trim()).Where(v => v.Length > 0).Distinct().ToList() })
            .Where(g => g.Name.Length > 0 && g.Values.Count > 0)
            .ToList();
        if (groups.Count == 0) throw new AppException("Add at least one option with at least one value.");

        var existing = await _db.ProductVariants.Include(v => v.Options)
            .Where(v => v.ProductId == productId)
            .ToListAsync(ct);
        var existingSignatures = existing.Select(v => Signature(v.Options.Select(o => (o.OptionName, o.OptionValue)))).ToHashSet();

        // Every reserved SKU right now, checked as each new one is minted so two new
        // variants in the same batch can never collide with each other either.
        var takenSkus = (await _db.ProductVariants.Select(v => v.Sku).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var added = 0;
        foreach (var combo in CartesianProduct(groups.Select(g => g.Values).ToList()))
        {
            var pairs = groups.Zip(combo, (g, value) => (g.Name, Value: value)).ToList();
            var signature = Signature(pairs.Select(p => (p.Name, p.Value)));
            if (!existingSignatures.Add(signature)) continue; // already exists — additive, leave it alone

            var sku = UniqueSku(product.Sku, pairs.Select(p => p.Value), takenSkus);
            takenSkus.Add(sku);

            _db.ProductVariants.Add(new ProductVariant
            {
                ProductId = productId,
                Sku = sku,
                Name = string.Join(" / ", pairs.Select(p => p.Value)),
                PriceAdjustment = 0m,
                IsActive = true,
                CreatedAt = now,
                Options = pairs.Select(p => new VariantOption { OptionName = p.Name, OptionValue = p.Value, CreatedAt = now }).ToList(),
            });
            added++;
        }

        if (added > 0) await _db.SaveChangesAsync(ct);
        return await ListAsync(productId, ct);
    }

    /// <summary>Order-independent identity for a set of option pairs, so the same combination
    /// requested in a different option order still matches an existing variant.</summary>
    private static string Signature(IEnumerable<(string Name, string Value)> pairs) =>
        string.Join("|", pairs
            .Select(p => $"{p.Name.Trim().ToLowerInvariant()}={p.Value.Trim().ToLowerInvariant()}")
            .OrderBy(s => s, StringComparer.Ordinal));

    private static string UniqueSku(string productSku, IEnumerable<string> values, HashSet<string> taken)
    {
        var baseSku = string.Join("-", new[] { productSku }.Concat(values).Select(Slug).Where(s => s.Length > 0));
        var sku = baseSku;
        var n = 2;
        while (taken.Contains(sku)) sku = $"{baseSku}-{n++}";
        return sku;
    }

    private static string Slug(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>All combinations, one value per input list, in list order (Quantity × Colour × Size, …).</summary>
    private static IEnumerable<List<string>> CartesianProduct(IReadOnlyList<List<string>> lists)
    {
        IEnumerable<List<string>> seed = [[]];
        return lists.Aggregate(seed, (acc, list) =>
            acc.SelectMany(prefix => list.Select(value => prefix.Append(value).ToList())));
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
