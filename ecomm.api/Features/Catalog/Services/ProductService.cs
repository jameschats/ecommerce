using ecomm.api.Common;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace ecomm.api.Features.Catalog.Services;

public interface IProductService
{
    Task<PagedResult<ProductListItemDto>> BrowseAsync(ProductQuery query, bool adminView, CancellationToken ct = default);
    Task<ProductDetailDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<ProductDetailDto> CreateAsync(SaveProductRequest req, long? userId, CancellationToken ct = default);
    Task<ProductDetailDto?> UpdateAsync(long id, SaveProductRequest req, long? userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class ProductService : IProductService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;

    public ProductService(EcommerceDbContext db) => _db = db;

    public async Task<PagedResult<ProductListItemDto>> BrowseAsync(ProductQuery query, bool adminView, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = _db.Products.Where(p => p.TenantId == Tenant && !p.IsDeleted);

        if (!adminView)
            q = q.Where(p => p.IsActive && p.Status == "Active");
        else if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(p => p.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            // MySQL FULLTEXT (boolean + prefix) on Name/ShortDescription/Description, with a LIKE fallback
            // for SKUs and short tokens that full-text ignores.
            var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 3).ToList();
            if (tokens.Count > 0)
            {
                var boolQuery = string.Join(' ', tokens.Select(t => $"+{t}*"));
                q = q.Where(p =>
                    EF.Functions.Match(new[] { p.Name, p.ShortDescription!, p.Description! }, boolQuery, MySqlMatchSearchMode.Boolean) > 0
                    || p.Sku.Contains(s)
                    || p.Category!.Name.Contains(s)
                    || (p.Brand != null && p.Brand.Name.Contains(s))
                    || p.AttributeValues.Any(av =>
                        (av.ValueText != null && av.ValueText.Contains(s)) || (av.Value != null && av.Value.Value.Contains(s))));
            }
            else
            {
                q = q.Where(p =>
                    p.Name.Contains(s) || p.Sku.Contains(s)
                    || p.Category!.Name.Contains(s)
                    || (p.Brand != null && p.Brand.Name.Contains(s))
                    || p.AttributeValues.Any(av =>
                        (av.ValueText != null && av.ValueText.Contains(s)) || (av.Value != null && av.Value.Value.Contains(s))));
            }
        }
        if (query.CategoryId is { } cat) q = q.Where(p => p.CategoryId == cat);
        if (query.BrandId is { } brand) q = q.Where(p => p.BrandId == brand);
        if (query.IsFeatured is { } feat) q = q.Where(p => p.IsFeatured == feat);

        q = query.Sort switch
        {
            "price" => q.OrderBy(p => p.Price),
            "price_desc" => q.OrderByDescending(p => p.Price),
            "name" => q.OrderBy(p => p.Name),
            _ => q.OrderByDescending(p => p.CreatedAt),
        };

        var total = await q.LongCountAsync(ct);
        var items = await q
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ProductListItemDto(
                p.ProductId, p.Sku, p.Name, p.Slug, p.Price, p.CompareAtPrice, p.Status, p.IsFeatured,
                p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                p.Category!.Name,
                p.Brand != null ? p.Brand.Name : null,
                p.InventoryRecords.Sum(i => i.AvailableQty) > 0))
            .ToListAsync(ct);

        return new PagedResult<ProductListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        };
    }

    public Task<ProductDetailDto?> GetByIdAsync(long id, CancellationToken ct = default) =>
        Project(_db.Products.Where(p => p.ProductId == id && p.TenantId == Tenant && !p.IsDeleted), ct);

    public Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        Project(_db.Products.Where(p => p.Slug == slug && p.TenantId == Tenant && !p.IsDeleted), ct);

    public async Task<ProductDetailDto> CreateAsync(SaveProductRequest req, long? userId, CancellationToken ct = default)
    {
        Validate(req);
        await EnsureCategoryExists(req.CategoryId, ct);
        var sku = req.Sku.Trim();
        if (await _db.Products.AnyAsync(p => p.TenantId == Tenant && p.Sku == sku, ct))
            throw new AppException($"SKU '{sku}' already exists.", StatusCodes.Status409Conflict);

        var now = DateTime.UtcNow;
        var product = new Product
        {
            TenantId = Tenant,
            Sku = sku,
            Name = req.Name.Trim(),
            Slug = await UniqueSlugAsync(req.Slug ?? req.Name, null, ct),
            CategoryId = req.CategoryId,
            BrandId = req.BrandId,
            ProductType = req.ProductType?.Trim(),
            Tags = NormalizeTags(req.Tags),
            ShortDescription = req.ShortDescription,
            Description = req.Description,
            MetaTitle = req.MetaTitle?.Trim(),
            MetaDescription = req.MetaDescription?.Trim(),
            HsnCode = req.HsnCode,
            Price = req.Price,
            CompareAtPrice = req.CompareAtPrice,
            CostPrice = req.CostPrice,
            Status = NormalizeStatus(req.Status),
            IsFeatured = req.IsFeatured,
            IsActive = true,
            CreatedBy = userId,
            CreatedAt = now,
            Images = BuildImages(req.Images, now),
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(product.ProductId, ct))!;
    }

    public async Task<ProductDetailDto?> UpdateAsync(long id, SaveProductRequest req, long? userId, CancellationToken ct = default)
    {
        var product = await _db.Products.Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.ProductId == id && p.TenantId == Tenant && !p.IsDeleted, ct);
        if (product is null) return null;

        Validate(req);
        await EnsureCategoryExists(req.CategoryId, ct);
        var sku = req.Sku.Trim();
        if (await _db.Products.AnyAsync(p => p.TenantId == Tenant && p.Sku == sku && p.ProductId != id, ct))
            throw new AppException($"SKU '{sku}' already exists.", StatusCodes.Status409Conflict);

        var now = DateTime.UtcNow;
        product.Sku = sku;
        product.Name = req.Name.Trim();
        product.Slug = await UniqueSlugAsync(req.Slug ?? req.Name, id, ct);
        product.CategoryId = req.CategoryId;
        product.BrandId = req.BrandId;
        product.ProductType = req.ProductType?.Trim();
        product.Tags = NormalizeTags(req.Tags);
        product.ShortDescription = req.ShortDescription;
        product.Description = req.Description;
        product.MetaTitle = req.MetaTitle?.Trim();
        product.MetaDescription = req.MetaDescription?.Trim();
        product.HsnCode = req.HsnCode;
        product.Price = req.Price;
        product.CompareAtPrice = req.CompareAtPrice;
        product.CostPrice = req.CostPrice;
        product.Status = NormalizeStatus(req.Status);
        product.IsFeatured = req.IsFeatured;
        product.UpdatedBy = userId;
        product.UpdatedAt = now;

        if (req.Images is not null)
        {
            _db.ProductImages.RemoveRange(product.Images);
            product.Images = BuildImages(req.Images, now);
        }

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(
            p => p.ProductId == id && p.TenantId == Tenant && !p.IsDeleted, ct);
        if (product is null) return false;

        product.IsDeleted = true;
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // --- helpers ---

    private static Task<ProductDetailDto?> Project(IQueryable<Product> q, CancellationToken ct) =>
        q.Select(p => new ProductDetailDto(
            p.ProductId, p.Sku, p.Name, p.Slug, p.ShortDescription, p.Description,
            p.Price, p.CompareAtPrice, p.CostPrice, p.HsnCode, p.Status, p.IsFeatured, p.IsActive,
            p.CategoryId, p.Category!.Name, p.BrandId, p.Brand != null ? p.Brand.Name : null,
            p.InventoryRecords.Sum(i => i.AvailableQty), p.InventoryRecords.Sum(i => i.AvailableQty) > 0,
            p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder)
                .Select(i => new ProductImageDto(i.ProductImageId, i.Url, i.AltText, i.DisplayOrder, i.IsPrimary))
                .ToList(),
            p.Variants.OrderBy(v => v.ProductVariantId)
                .Select(v => new ProductVariantDto(v.ProductVariantId, v.Sku, v.Name, v.PriceAdjustment, v.IsActive,
                    v.Options.Select(o => new VariantOptionDto(o.OptionName, o.OptionValue)).ToList()))
                .ToList(),
            p.AttributeValues
                .Select(a => new ProductAttributeValueDto(a.ProductAttributeValueId, a.AttributeId, a.Attribute!.Name,
                    a.AttributeValueId, a.Value != null ? a.Value.Value : null, a.ValueText))
                .ToList(),
            p.ProductType, p.Tags, p.MetaTitle, p.MetaDescription))
        .FirstOrDefaultAsync(ct);

    private static string? NormalizeTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags) ? null
        : string.Join(",", tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct());

    private static void Validate(SaveProductRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Sku)) throw new AppException("SKU is required.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Name is required.");
        if (req.Price < 0) throw new AppException("Price cannot be negative.");
    }

    private async Task EnsureCategoryExists(long categoryId, CancellationToken ct)
    {
        if (!await _db.Categories.AnyAsync(c => c.CategoryId == categoryId && c.TenantId == Tenant, ct))
            throw new AppException("The selected category does not exist.");
    }

    private static string NormalizeStatus(string? status) =>
        status is "Active" or "Inactive" or "Draft" ? status : "Draft";

    private static List<ProductImage> BuildImages(IReadOnlyList<ProductImageInput>? images, DateTime now) =>
        (images ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Url))
            .Select(i => new ProductImage
            {
                Url = i.Url.Trim(),
                AltText = i.AltText,
                DisplayOrder = i.DisplayOrder,
                IsPrimary = i.IsPrimary,
                MediaFileId = i.MediaFileId,
                CreatedAt = now,
            })
            .ToList();

    private async Task<string> UniqueSlugAsync(string source, long? excludeId, CancellationToken ct)
    {
        var baseSlug = Slug.From(source);
        var slug = baseSlug;
        var exclude = excludeId ?? 0;
        var n = 1;
        while (await _db.Products.AnyAsync(
            p => p.TenantId == Tenant && p.Slug == slug && p.ProductId != exclude, ct))
        {
            slug = $"{baseSlug}-{++n}";
        }
        return slug;
    }
}
