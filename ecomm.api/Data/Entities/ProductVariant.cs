namespace ecomm.api.Data.Entities;

public class ProductVariant
{
    public long ProductVariantId { get; set; }
    public long ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Name { get; set; }              // e.g. "XL / Black"
    public decimal PriceAdjustment { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Product? Product { get; set; }
    public ICollection<VariantOption> Options { get; set; } = new List<VariantOption>();
}
