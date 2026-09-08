namespace ecomm.api.Data.Entities;

/// <summary>
/// What the customer actually typed into a <see cref="ProductCustomField"/>, snapshotted onto
/// the order item at sale time — editing or deleting the field definition afterwards must not
/// change what an already-placed order shows.
/// </summary>
public class OrderItemCustomFieldValue
{
    public long OrderItemCustomFieldValueId { get; set; }
    public long OrderItemId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public OrderItem? OrderItem { get; set; }
}
