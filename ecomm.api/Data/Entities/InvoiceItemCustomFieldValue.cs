namespace ecomm.api.Data.Entities;

/// <summary>
/// Same snapshot as <see cref="OrderItemCustomFieldValue"/>, copied onto the invoice item when
/// the invoice is generated — <see cref="InvoiceItem"/> already duplicates ProductName/DesignNo/
/// HsnCode rather than referencing OrderItems, so this follows the same convention.
/// </summary>
public class InvoiceItemCustomFieldValue
{
    public long InvoiceItemCustomFieldValueId { get; set; }
    public long InvoiceItemId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public InvoiceItem? InvoiceItem { get; set; }
}
