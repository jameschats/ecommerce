namespace ecomm.api.Features.PublicApi;

/// <summary>
/// Deliberately separate, hand-maintained response shapes for the public API — never the internal
/// admin DTOs directly. The internal shapes (<c>ProductDetailDto</c>, <c>OrderDto</c>, ...) change
/// freely as the Angular admin app's own needs evolve; a third-party integrator depending on those
/// directly would break on every internal refactor. These types are the public contract and change
/// only with real versioning discipline (a new <c>/api/public/v2/</c>, not an in-place edit).
/// </summary>
public sealed record PublicProductDto(
    long Id, string Sku, string Name, string Slug, decimal Price, decimal? CompareAtPrice,
    string Status, bool InStock, int AvailableQty, string? CategoryName, string? BrandName,
    IReadOnlyList<string> ImageUrls);

public sealed record PublicOrderItemDto(long ProductId, string ProductName, string? Sku, int Quantity, decimal UnitPrice, decimal LineTotal);

/// <summary>List view — no per-order extra query for items/shipment (that's a real N+1 for a
/// paginated endpoint). <see cref="PublicOrderDto"/> is the full shape, one query, for GET-by-id.</summary>
public sealed record PublicOrderSummaryDto(long Id, string OrderNumber, string Status, decimal TotalAmount, int ItemCount, DateTime? PlacedAt);

public sealed record PublicOrderDto(
    long Id, string OrderNumber, string Status, string Currency, decimal TotalAmount,
    DateTime? PlacedAt, IReadOnlyList<PublicOrderItemDto> Items, string? ShipmentStatus, string? TrackingNumber);

public sealed record PublicInventoryDto(long ProductId, string Sku, int AvailableQty, int ReservedQty, int ReorderLevel, bool IsLowStock);
public sealed record UpdateInventoryRequest(int AvailableQty, int ReorderLevel);
