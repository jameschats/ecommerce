namespace ecomm.api.Data.Entities;

/// <summary>Maps to the `Attributes` table. Named to avoid clashing with System.Attribute.</summary>
public class AttributeDefinition
{
    public long AttributeId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Name { get; set; } = string.Empty;     // Warranty | Voltage | Pages
    public string Code { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";      // string | int | decimal | bool
    public bool IsFilterable { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<AttributeValue> Values { get; set; } = new List<AttributeValue>();
}
