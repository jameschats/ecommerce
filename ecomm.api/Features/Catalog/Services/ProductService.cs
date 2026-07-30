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
    Task<PriceListDto> GetPriceListAsync(CancellationToken ct = default);
    Task<ProductDetailDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<ProductDetailDto> CreateAsync(SaveProductRequest req, long? userId, CancellationToken ct = default);
    Task<ProductDetailDto?> UpdateAsync(long id, SaveProductRequest req, long? userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    Task<BulkProductActionResult> BulkAsync(BulkProductActionRequest req, CancellationToken ct = default);
}

public sealed class ProductService : IProductService
{
    private const long Tenant = 1;

    /// <summary>Attribute holding the pack unit shown in the price list's "Content" column.</summary>
    private const string ContentAttributeName = "Content";
    private readonly EcommerceDbContext _db;

    public ProductService(EcommerceDbContext db) => _db = db;

    /// <summary>
    /// The entire active catalogue in one payload, grouped into category bands for the
    /// quick-order table (design.md §5). Unpaged by design — a dealer tabs down the whole
    /// price list, so paging it would break both the workflow and Ctrl+F.
    ///
    /// Categories with their own page (Finished Calendar) are included here rather than
    /// filtered out, and carry ShowInPriceList so each screen can pick its own bands. The
    /// client indexes this payload to resolve the quantities it has stored and prunes
    /// anything missing from it — so serving a narrowed list to one page would delete the
    /// quantities typed on another.
    /// </summary>
    public async Task<PriceListDto> GetPriceListAsync(CancellationToken ct = default)
    {
        var rows = await _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted && p.IsActive && p.Status == "Active")
            .OrderBy(p => p.Category!.DisplayOrder).ThenBy(p => p.Category!.Name).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.ProductId,
                // Design No is what the price list shows; SKU is the fallback for products
                // that predate the catalogue import and have no design number yet.
                Sku = p.DesignNo != null && p.DesignNo != "" ? p.DesignNo : p.Sku,
                p.Name,
                p.Price,
                p.CompareAtPrice,
                CategoryId = p.Category!.CategoryId,
                CategoryName = p.Category!.Name,
                CategorySlug = p.Category!.Slug,
                CategoryOrder = p.Category!.DisplayOrder,
                CategoryShowInPriceList = p.Category!.ShowInPriceList,
                ParentCategoryName = p.Category!.Parent != null ? p.Category!.Parent.Name : null,
                // Optional per-product pack unit. Either a predefined attribute value or free text.
                Content = p.AttributeValues
                    .Where(av => av.Attribute!.Name == ContentAttributeName)
                    .Select(av => av.ValueText ?? (av.Value != null ? av.Value.Value : null))
                    .FirstOrDefault(),
                ImageUrl = p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder)
                    .Select(i => i.Url).FirstOrDefault(),
                InStock = p.InventoryRecords.Sum(i => i.AvailableQty) > 0,
            })
            .ToListAsync(ct);

        var bands = rows
            .GroupBy(r => new { r.CategoryId, r.CategoryName, r.CategorySlug, r.ParentCategoryName, r.CategoryOrder, r.CategoryShowInPriceList })
            .OrderBy(g => g.Key.CategoryOrder).ThenBy(g => g.Key.CategoryName)
            .Select(g => new PriceListBandDto(
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Key.CategorySlug,
                g.Key.ParentCategoryName,
                // "WALL CALENDARS — 12 x 18" when the category has a parent, else just its own name.
                g.Key.ParentCategoryName is null
                    ? g.Key.CategoryName
                    : $"{g.Key.ParentCategoryName} — {g.Key.CategoryName}",
                g.Key.CategoryShowInPriceList,
                g.Select(r => new PriceListItemDto(
                    r.ProductId, r.Sku, r.Name, r.Content,
                    r.Price, r.CompareAtPrice,
                    DiscountPercent(r.Price, r.CompareAtPrice),
                    r.ImageUrl, r.InStock)).ToList()))
            .ToList();

        return new PriceListDto(bands, rows.Count);
    }

    /// <summary>Whole-percent saving off MRP. Zero when there is no MRP or it is not above the price.</summary>
    private static int DiscountPercent(decimal price, decimal? compareAt)
        => compareAt is > 0 && compareAt > price
            ? (int)Math.Round((compareAt.Value - price) / compareAt.Value * 100)
            : 0;

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
            // for SKUs, design numbers and short tokens that full-text ignores. Design No is matched
            // because it is the identifier the admin list and the price list both show — a number you
            // can read off the screen has to be a number you can search for.
            var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 3).ToList();
            if (tokens.Count > 0)
            {
                var boolQuery = string.Join(' ', tokens.Select(t => $"+{t}*"));
                q = q.Where(p =>
                    EF.Functions.Match(new[] { p.Name, p.ShortDescription!, p.Description! }, boolQuery, MySqlMatchSearchMode.Boolean) > 0
                    || p.Sku.Contains(s)
                    || (p.DesignNo != null && p.DesignNo.Contains(s))
                    || p.Category!.Name.Contains(s)
                    || (p.Brand != null && p.Brand.Name.Contains(s))
                    || p.AttributeValues.Any(av =>
                        (av.ValueText != null && av.ValueText.Contains(s)) || (av.Value != null && av.Value.Value.Contains(s))));
            }
            else
            {
                q = q.Where(p =>
                    p.Name.Contains(s) || p.Sku.Contains(s)
                    || (p.DesignNo != null && p.DesignNo.Contains(s))
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
                p.ProductId, p.Sku, p.DesignNo, p.Name, p.Slug, p.Price, p.CompareAtPrice, p.Status, p.IsFeatured,
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
            ShortDescription = req.ShortDescription,
            Description = req.Description,
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
        product.DesignNo = string.IsNullOrWhiteSpace(req.DesignNo) ? null : req.DesignNo.Trim();
        product.Name = req.Name.Trim();
        product.Slug = await UniqueSlugAsync(req.Slug ?? req.Name, id, ct);
        product.CategoryId = req.CategoryId;
        product.BrandId = req.BrandId;
        product.ShortDescription = req.ShortDescription;
        product.Description = req.Description;
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

    /// <summary>
    /// Applies one action to many products (design.md §10.3).
    ///
    /// Delete is soft, matching <see cref="DeleteAsync"/> — a bulk operation must not be
    /// more destructive than doing the same thing one row at a time.
    /// </summary>
    public async Task<BulkProductActionResult> BulkAsync(BulkProductActionRequest req, CancellationToken ct = default)
    {
        var ids = (req.ProductIds ?? []).Distinct().ToList();
        if (ids.Count == 0) throw new AppException("Select at least one product.");

        var products = await _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted && ids.Contains(p.ProductId))
            .ToListAsync(ct);

        if (products.Count == 0) throw new AppException("None of those products were found.");

        var now = DateTime.UtcNow;
        string summary;

        switch (req.Action?.ToLowerInvariant())
        {
            case "delete":
                foreach (var p in products) { p.IsDeleted = true; p.IsActive = false; p.UpdatedAt = now; }
                summary = $"{products.Count} product(s) deleted.";
                break;

            case "status":
            {
                var status = req.Status?.Trim();
                if (status is not ("Active" or "Draft" or "Inactive"))
                    throw new AppException("Status must be Active, Draft or Inactive.");
                foreach (var p in products)
                {
                    p.Status = status;
                    // Keep IsActive consistent with Status; the storefront filters on both,
                    // so letting them disagree makes a product invisible for no clear reason.
                    p.IsActive = status == "Active";
                    p.UpdatedAt = now;
                }
                summary = $"{products.Count} product(s) set to {status}.";
                break;
            }

            case "category":
            {
                if (req.CategoryId is not { } categoryId)
                    throw new AppException("Choose a category.");
                var exists = await _db.Categories.AnyAsync(
                    c => c.CategoryId == categoryId && c.TenantId == Tenant && c.IsActive, ct);
                if (!exists) throw new AppException("That category does not exist or is inactive.");

                foreach (var p in products) { p.CategoryId = categoryId; p.UpdatedAt = now; }
                summary = $"{products.Count} product(s) moved.";
                break;
            }

            case "price":
            case "mrp":
            case "cost":
            {
                if (req.Amount is not { } amount) throw new AppException("Enter an amount.");
                var mode = req.Mode?.ToLowerInvariant() ?? "set";

                foreach (var p in products)
                {
                    var current = req.Action.ToLowerInvariant() switch
                    {
                        "price" => p.Price,
                        "mrp" => p.CompareAtPrice ?? 0m,
                        _ => p.CostPrice ?? 0m,
                    };

                    var next = mode switch
                    {
                        "byamount" => current + amount,
                        "bypercent" => current * (1 + amount / 100m),
                        _ => amount,
                    };

                    // A negative price is never a legitimate outcome — clamp rather than
                    // write nonsense that would then be charged to a customer.
                    next = Math.Max(0m, next);
                    next = req.RoundToWhole
                        ? Math.Round(next, 0, MidpointRounding.AwayFromZero)
                        : Math.Round(next, 2, MidpointRounding.AwayFromZero);

                    switch (req.Action.ToLowerInvariant())
                    {
                        case "price": p.Price = next; break;
                        case "mrp": p.CompareAtPrice = next; break;
                        default: p.CostPrice = next; break;
                    }
                    p.UpdatedAt = now;
                }

                var label = req.Action.ToLowerInvariant() switch
                {
                    "price" => "discounted price", "mrp" => "MRP", _ => "cost",
                };
                summary = $"{label} updated on {products.Count} product(s).";
                break;
            }

            default:
                throw new AppException($"Unknown bulk action '{req.Action}'.");
        }

        await _db.SaveChangesAsync(ct);
        return new BulkProductActionResult(products.Count, summary);
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
            p.ProductId, p.Sku, p.DesignNo, p.Name, p.Slug, p.ShortDescription, p.Description,
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
                .ToList()))
        .FirstOrDefaultAsync(ct);

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
