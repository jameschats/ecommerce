namespace ecomm.api.Data.Entities;

public class InvoiceItem
{
    public long InvoiceItemId { get; set; }
    public long InvoiceId { get; set; }
    public long? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? DesignNo { get; set; }   // trade design number, snapshotted at sale time
    public string? HsnCode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime CreatedAt { get; set; }

    public Invoice? Invoice { get; set; }
}
