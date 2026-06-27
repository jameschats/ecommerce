namespace ecomm.api.Data.Entities;

public class CartItem
{
    public long CartItemId { get; set; }
    public long CartId { get; set; }
    public long ProductId { get; set; }
    public long? ProductVariantId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }      // snapshot at add time
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Cart? Cart { get; set; }
    public Product? Product { get; set; }
}
