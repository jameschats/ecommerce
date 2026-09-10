namespace ecomm.api.Data.Entities;

/// <summary>Admin-defined personalization field on a product (e.g. "Mention your design number").
/// Definition only — storefront capture and cart/order persistence of the customer's typed value
/// is a separate, later pass.</summary>
public class ProductCustomTextField
{
    public long ProductCustomTextFieldId { get; set; }
    public long ProductId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int MaxLength { get; set; } = 255;
    public bool IsMandatory { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Product? Product { get; set; }
}
