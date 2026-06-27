namespace ecomm.api.Features.Catalog.Dtos;

// --- Variants ---
public sealed record VariantOptionDto(string OptionName, string OptionValue);
public sealed record ProductVariantDto(
    long ProductVariantId, string Sku, string? Name, decimal PriceAdjustment, bool IsActive,
    IReadOnlyList<VariantOptionDto> Options);
public sealed record SaveVariantRequest(
    string Sku, string? Name, decimal PriceAdjustment, bool IsActive, IReadOnlyList<VariantOptionDto>? Options);

// --- Attribute definitions ---
public sealed record AttributeValueDto(long AttributeValueId, string Value);
public sealed record AttributeDto(
    long AttributeId, string Name, string Code, string DataType, bool IsFilterable, bool IsActive,
    IReadOnlyList<AttributeValueDto> Values);
public sealed record SaveAttributeRequest(string Name, string? Code, string DataType, bool IsFilterable, bool IsActive);
public sealed record SaveAttributeValueRequest(string Value);

// --- Product attribute assignment ---
public sealed record ProductAttributeValueDto(
    long ProductAttributeValueId, long AttributeId, string AttributeName,
    long? AttributeValueId, string? Value, string? ValueText);
public sealed record ProductAttributeInput(long AttributeId, long? AttributeValueId, string? ValueText);
public sealed record SetProductAttributesRequest(IReadOnlyList<ProductAttributeInput> Attributes);

// --- Import jobs ---
public sealed record ImportJobItemDto(int RowNumber, string Status, string? ErrorMessage);
public sealed record ImportJobDto(
    long ImportJobId, string JobType, string? FileName, string Status,
    int TotalRows, int SuccessRows, int FailedRows, DateTime CreatedAt, DateTime? CompletedAt);
public sealed record ImportResultDto(ImportJobDto Job, IReadOnlyList<ImportJobItemDto> FailedRows);
