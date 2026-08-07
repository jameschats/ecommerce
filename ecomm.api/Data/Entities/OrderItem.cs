namespace ecomm.api.Data.Entities;

public class OrderItem
{
    public long OrderItemId { get; set; }
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public long? ProductVariantId { get; set; }
    public string? Sku { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? DesignNo { get; set; }   // trade design number, snapshotted at sale time
    public string? HsnCode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? UnitCost { get; set; }   // cost snapshot at sale time (for margin reports)
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime CreatedAt { get; set; }

    public Order? Order { get; set; }
}
