namespace ecomm.api.Data.Entities;

public class Inventory : ITenantScoped
{
    public long InventoryId { get; set; }
    public long TenantId { get; set; } = 1;
    public long ProductId { get; set; }
    public long? ProductVariantId { get; set; }
    public int AvailableQty { get; set; }
    public int ReservedQty { get; set; }
    public int ReorderLevel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Product? Product { get; set; }
}
