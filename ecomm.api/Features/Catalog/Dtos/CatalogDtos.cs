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
    string Status, bool IsFeatured, string? PrimaryImageUrl, string CategoryName, string? BrandName, bool InStock,
    int AvailableQty, bool IsLowStock, IReadOnlyList<string> ColorOptions,
    DateTime CreatedAt, string? SecondaryImageUrl,
    double Rating = 0, int ReviewCount = 0);

public sealed record ProductDetailDto(
    long ProductId, string Sku, string Name, string Slug, string? ShortDescription, string? Description,
    decimal Price, decimal? CompareAtPrice, decimal? CostPrice, string? HsnCode, string Status,
    bool IsFeatured, bool IsActive, long CategoryId, string CategoryName, long? BrandId, string? BrandName,
    int AvailableQty, bool InStock,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductAttributeValueDto> Attributes,
    string? ProductType, string? Tags, string? MetaTitle, string? MetaDescription, bool IsBundle = false);

public sealed record SaveProductRequest(
    string Sku, string Name, string? Slug, long CategoryId, long? BrandId, decimal Price,
    decimal? CompareAtPrice, decimal? CostPrice, string? ShortDescription, string? Description,
    string? HsnCode, string Status, bool IsFeatured, IReadOnlyList<ProductImageInput>? Images,
    string? ProductType = null, string? Tags = null, string? MetaTitle = null, string? MetaDescription = null,
    bool IsBundle = false);

public sealed record ProductQuery(
    string? Search, long? CategoryId, long? BrandId, string? Status, bool? IsFeatured,
    string? Sort, int Page = 1, int PageSize = 20, IReadOnlyList<long>? Ids = null,
    decimal? MinPrice = null, decimal? MaxPrice = null,
    // Faceted filters (storefront PLP). Multi-value facets are OR within, AND across.
    IReadOnlyList<long>? BrandIds = null,       // ?brandIds=1&brandIds=2
    IReadOnlyList<string>? Color = null,        // ?color=Blue&color=Red (variant options)
    IReadOnlyList<string>? Size = null,         // ?size=XL (variant options)
    IReadOnlyList<string>? Attr = null,         // ?attr=fabric:Silk&attr=fabric:Cotton  ("code:value")
    bool? InStock = null, bool? OnSale = null, int? MinRating = null);

// ---- Facets (available filter values + counts for the current result set) ----
public sealed record BrandFacetDto(long BrandId, string Name, int Count);
public sealed record ValueFacetDto(string Value, int Count, string? Hex = null);
public sealed record AttributeFacetDto(string Code, string Name, IReadOnlyList<ValueFacetDto> Values);
public sealed record FacetsDto(
    int Total,
    IReadOnlyList<BrandFacetDto> Brands,
    IReadOnlyList<ValueFacetDto> Colors,
    IReadOnlyList<ValueFacetDto> Sizes,
    IReadOnlyList<AttributeFacetDto> Attributes,
    decimal PriceMin, decimal PriceMax,
    IReadOnlyList<int> RatingCounts,   // index 0 = 1★+, … index 4 = 5★
    int InStockCount, int OnSaleCount);

public sealed record NotifyBackInStockRequest(string Email);
