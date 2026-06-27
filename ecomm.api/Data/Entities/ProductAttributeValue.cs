namespace ecomm.api.Data.Entities;

public class ProductAttributeValue
{
    public long ProductAttributeValueId { get; set; }
    public long ProductId { get; set; }
    public long AttributeId { get; set; }
    public long? AttributeValueId { get; set; }   // predefined value, if any
    public string? ValueText { get; set; }         // free-form value, if any
    public DateTime CreatedAt { get; set; }

    public Product? Product { get; set; }
    public AttributeDefinition? Attribute { get; set; }
    public AttributeValue? Value { get; set; }
}
