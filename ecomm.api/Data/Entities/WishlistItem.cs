namespace ecomm.api.Data.Entities;

/// <summary>A product a customer saved to their wishlist. One row per (user, product).</summary>
public class WishlistItem
{
    public long WishlistItemId { get; set; }
    public long TenantId { get; set; } = 1;
    public long UserId { get; set; }
    public long ProductId { get; set; }
    public DateTime CreatedAt { get; set; }
}
