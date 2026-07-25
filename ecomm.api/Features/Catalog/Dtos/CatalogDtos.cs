namespace ecomm.api.Features.Catalog.Dtos;

// --- Category ---
public sealed record CategoryDto(
    long CategoryId, long? ParentCategoryId, string Name, string Slug,
    string? Description, string? ImageUrl, int DisplayOrder, bool IsActive);

public sealed record SaveCategoryRequest(
    string Name, long? ParentCategoryId, string? Slug, string? Description,
    string? ImageUrl, int DisplayOrder, bool IsActive);

// --- Brand ---
public sealed record BrandDto(
    long BrandId, string Name, string Slug, string? LogoUrl, string? Description, bool IsActive);

public sealed record SaveBrandRequest(
    string Name, string? Slug, string? LogoUrl, string? Description, bool IsActive);

// --- Product ---
public sealed record ProductImageDto(long ProductImageId, string Url, string? AltText, int DisplayOrder, bool IsPrimary);
public sealed record ProductImageInput(string Url, string? AltText, int DisplayOrder, bool IsPrimary, long? MediaFileId = null);

public sealed record ProductListItemDto(
    long ProductId, string Sku, string Name, string Slug, decimal Price, decimal? CompareAtPrice,
    string Status, bool IsFeatured, string? PrimaryImageUrl, string CategoryName, string? BrandName, bool InStock);

public sealed record ProductDetailDto(
    long ProductId, string Sku, string? DesignNo, string Name, string Slug, string? ShortDescription, string? Description,
    decimal Price, decimal? CompareAtPrice, decimal? CostPrice, string? HsnCode, string Status,
    bool IsFeatured, bool IsActive, long CategoryId, string CategoryName, long? BrandId, string? BrandName,
    int AvailableQty, bool InStock,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductAttributeValueDto> Attributes);

public sealed record SaveProductRequest(
    string Sku, string? DesignNo, string Name, string? Slug, long CategoryId, long? BrandId, decimal Price,
    decimal? CompareAtPrice, decimal? CostPrice, string? ShortDescription, string? Description,
    string? HsnCode, string Status, bool IsFeatured, IReadOnlyList<ProductImageInput>? Images);

public sealed record ProductQuery(
    string? Search, long? CategoryId, long? BrandId, string? Status, bool? IsFeatured,
    string? Sort, int Page = 1, int PageSize = 20);

// ---------------------------------------------------------------------------
// Bulk actions on the admin product list.
//
// Editing 400 designs one at a time is not a workflow. These cover the operations that
// are genuinely painful individually: retiring a range, moving designs between categories,
// and annual price revisions.
// ---------------------------------------------------------------------------

/// <param name="Action">Delete | Status | Category | Price | Mrp | Cost</param>
/// <param name="Mode">
/// How <paramref name="Amount"/> applies to the price actions: <c>Set</c> replaces the
/// value, <c>ByAmount</c> adds it (negative to reduce), <c>ByPercent</c> scales by it.
/// </param>
public sealed record BulkProductActionRequest(
    IReadOnlyList<long> ProductIds,
    string Action,
    string? Status = null,
    long? CategoryId = null,
    decimal? Amount = null,
    string? Mode = null,
    bool RoundToWhole = false);

public sealed record BulkProductActionResult(int Affected, string Summary);

// ---------------------------------------------------------------------------
// Quick-order price list (Phase 1). The whole catalogue in one payload, grouped
// into category bands — see documents/stages-v2/design.md §5.
//
// Deliberately lean: 400+ rows ship in a single response, so every field here is
// one the table actually renders. No slug, no description, no brand.
// ---------------------------------------------------------------------------

/// <summary>One row of the quick-order table.</summary>
/// <param name="Sku">Shown as "Design No" — the identifier the trade actually uses.</param>
/// <param name="Content">Pack unit ("1 Box (50 Pcs)"), from the optional `Content` attribute.</param>
/// <param name="DiscountPercent">
/// Derived from MRP vs price, never stored — so it cannot drift out of step with the
/// prices it describes.
/// </param>
public sealed record PriceListItemDto(
    long ProductId, string Sku, string Name, string? Content,
    decimal Price, decimal? CompareAtPrice, int DiscountPercent,
    string? ImageUrl, bool InStock);

/// <summary>A full-width category band and the rows under it.</summary>
public sealed record PriceListBandDto(
    long CategoryId, string CategoryName, string CategorySlug,
    string? ParentCategoryName, string Label,
    IReadOnlyList<PriceListItemDto> Items);

public sealed record PriceListDto(IReadOnlyList<PriceListBandDto> Bands, int TotalItems);
