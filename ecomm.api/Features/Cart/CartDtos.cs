namespace ecomm.api.Features.Cart;

public sealed record CartItemDto(
    long CartItemId, long ProductId, long? ProductVariantId,
    string Name, string Slug, string? ImageUrl, string? VariantLabel,
    decimal UnitPrice, int Quantity, decimal LineTotal,
    int AvailableQty, bool InStock);

public sealed record CartDto(
    long CartId, IReadOnlyList<CartItemDto> Items,
    int ItemCount, int DistinctCount, decimal Subtotal);

public sealed record AddToCartRequest(long ProductId, long? ProductVariantId, int Quantity);
public sealed record UpdateCartItemRequest(int Quantity);
