namespace ecomm.api.Data.Entities;

public class CreditNoteItem
{
    public long CreditNoteItemId { get; set; }
    public long CreditNoteId { get; set; }
    public long? ProductId { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime CreatedAt { get; set; }

    public CreditNote? CreditNote { get; set; }
}
