namespace ecomm.api.Features.Catalog.Dtos;

// --- Variants ---
public sealed record VariantOptionDto(string OptionName, string OptionValue);
public sealed record ProductVariantDto(
    long ProductVariantId, string Sku, string? Name, decimal PriceAdjustment, bool IsActive,
    IReadOnlyList<VariantOptionDto> Options);
public sealed record SaveVariantRequest(
    string Sku, string? Name, decimal PriceAdjustment, bool IsActive, IReadOnlyList<VariantOptionDto>? Options);

/// <summary>One option group and the values it can take — e.g. ("Quantity", ["100","200","500"]).</summary>
public sealed record GenerateVariantsOptionInput(string Name, IReadOnlyList<string> Values);

/// <summary>
/// Generates every combination across the given option groups as a variant — e.g. Quantity ×
/// Colour × Size with 5, 4 and 2 values respectively yields 40 variants. Additive: a
/// combination that already exists as a variant (regardless of which request created it) is
/// left untouched rather than duplicated, so re-running this after adding one more value to
/// one group only creates the new combinations, never touches SKU/price/stock already set on
/// the existing ones.
/// </summary>
public sealed record GenerateVariantsRequest(IReadOnlyList<GenerateVariantsOptionInput> Options);

// --- Custom text fields (admin-defined, customer-answered — see ProductCustomFieldService) ---
public sealed record ProductCustomFieldDto(long ProductCustomFieldId, string Label, int CharLimit, bool IsMandatory, int SortOrder);
public sealed record SaveProductCustomFieldInput(string Label, int CharLimit, bool IsMandatory, int SortOrder);
public sealed record SetProductCustomFieldsRequest(IReadOnlyList<SaveProductCustomFieldInput> Fields);

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

/// <summary>
/// Dry-run result: what an import *would* do, before anything is written (design.md §10.1).
/// </summary>
public sealed record ImportPreviewDto(
    int TotalRows,
    int NewProducts,
    int UpdatedProducts,
    int ErrorCount,
    IReadOnlyList<ImportJobItemDto> Errors,
    IReadOnlyList<string> NewCategories,
    IReadOnlyList<string> Duplicates,
    IReadOnlyList<string> AttributeColumns);
