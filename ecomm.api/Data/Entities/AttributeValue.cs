namespace ecomm.api.Data.Entities;

public class AttributeValue
{
    public long AttributeValueId { get; set; }
    public long AttributeId { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public AttributeDefinition? Attribute { get; set; }
}
