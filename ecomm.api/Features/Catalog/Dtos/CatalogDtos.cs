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
    DateTime CreatedAt, string? SecondaryImageUrl);

public sealed record ProductDetailDto(
    long ProductId, string Sku, string Name, string Slug, string? ShortDescription, string? Description,
    decimal Price, decimal? CompareAtPrice, decimal? CostPrice, string? HsnCode, string Status,
    bool IsFeatured, bool IsActive, long CategoryId, string CategoryName, long? BrandId, string? BrandName,
    int AvailableQty, bool InStock,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductAttributeValueDto> Attributes,
    string? ProductType, string? Tags, string? MetaTitle, string? MetaDescription);

public sealed record SaveProductRequest(
    string Sku, string Name, string? Slug, long CategoryId, long? BrandId, decimal Price,
    decimal? CompareAtPrice, decimal? CostPrice, string? ShortDescription, string? Description,
    string? HsnCode, string Status, bool IsFeatured, IReadOnlyList<ProductImageInput>? Images,
    string? ProductType = null, string? Tags = null, string? MetaTitle = null, string? MetaDescription = null);

public sealed record ProductQuery(
    string? Search, long? CategoryId, long? BrandId, string? Status, bool? IsFeatured,
    string? Sort, int Page = 1, int PageSize = 20, IReadOnlyList<long>? Ids = null);
