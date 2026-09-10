namespace ecomm.api.Data.Entities;

public class Product : ITenantScoped
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
    public string? ProductType { get; set; }
    public bool IsBundle { get; set; }   // a merchandised kit/combo — its own price/images/PDP, no own stock; see BundleItem
    public string? Tags { get; set; }                // comma-separated
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? HsnCode { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? CostPrice { get; set; }
    /// <summary>Dynamic Pricing (v4 Phase 5) bounds — the engine may never suggest outside these,
    /// and a product with either unset never receives a suggestion at all (no implicit bounds).</summary>
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    /// <summary>When true, the pricing engine skips this product entirely at generation time — a
    /// locked product never even produces a pending suggestion to review, not just "would be rejected."</summary>
    public bool PriceLocked { get; set; }
    public string Status { get; set; } = "Draft";   // Draft | Active | Inactive
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Merchant control: never surface this product in any recommendation placement (AI Commerce C4).</summary>
    public bool ExcludeFromRecommendations { get; set; }
    /// <summary>Merchant control: always surface this product in recommendation placements (AI Commerce C4).</summary>
    public bool PinnedInRecommendations { get; set; }
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
    public ICollection<ProductCustomTextField> CustomTextFields { get; set; } = new List<ProductCustomTextField>();
    public ICollection<Inventory> InventoryRecords { get; set; } = new List<Inventory>();
}
