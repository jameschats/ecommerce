namespace ecomm.api.Data.Entities;

public class Product
{
    public long ProductId { get; set; }
    public long TenantId { get; set; } = 1;
    public long? SellerId { get; set; }
    public long CategoryId { get; set; }
    public long? BrandId { get; set; }
    public long? TaxRateId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? HsnCode { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public string Status { get; set; } = "Draft";   // Draft | Active | Inactive
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Category? Category { get; set; }
    public Brand? Brand { get; set; }
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductAttributeValue> AttributeValues { get; set; } = new List<ProductAttributeValue>();
    public ICollection<Inventory> InventoryRecords { get; set; } = new List<Inventory>();
}
