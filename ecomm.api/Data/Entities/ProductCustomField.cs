namespace ecomm.api.Data.Entities;

/// <summary>
/// Admin-defined "custom text" field on a product — e.g. "Mention Correct Design number" —
/// answered by the customer on the product page and snapshotted onto the order item at sale
/// time (see <see cref="OrderItemCustomFieldValue"/>).
/// </summary>
public class ProductCustomField
{
    public long ProductCustomFieldId { get; set; }
    public long TenantId { get; set; } = 1;
    public long ProductId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int CharLimit { get; set; } = 500;
    public bool IsMandatory { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Product? Product { get; set; }
}
