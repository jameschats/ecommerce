namespace ecomm.api.Features.Inventory;

public sealed record InventoryRowDto(
    long ProductId, string Sku, string Name,
    int AvailableQty, int ReservedQty, int ReorderLevel, bool IsLowStock, bool HasVariants);

public sealed record VariantInventoryDto(
    long ProductVariantId, string Sku, string? Name,
    int AvailableQty, int ReservedQty, int ReorderLevel, bool IsLowStock);

public sealed record InventoryTransactionDto(
    long InventoryTransactionId, int ChangeQty, int? BalanceAfter, string TransactionType, string? Notes, DateTime CreatedAt);

public sealed record SetStockRequest(int AvailableQty, int ReorderLevel);
public sealed record AdjustStockRequest(int ChangeQty, string? Notes);
public sealed record InventoryQuery(string? Search, bool LowStockOnly = false, int Page = 1, int PageSize = 20);
