namespace ecomm.api.Data.Entities;

public class VariantOption
{
    public long VariantOptionId { get; set; }
    public long ProductVariantId { get; set; }
    public string OptionName { get; set; } = string.Empty;   // Size | Color | Capacity
    public string OptionValue { get; set; } = string.Empty;  // XL | Black | 64Wh
    public DateTime CreatedAt { get; set; }

    public ProductVariant? Variant { get; set; }
}
