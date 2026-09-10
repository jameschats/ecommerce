namespace ecomm.api.Data.Entities;

/// <summary>Maps onto the `CreditNotes` table (created empty in migration 007_billing.sql, unused by
/// any app code until now). A reversal document against an order/invoice.</summary>
public class CreditNote : ITenantScoped
{
    public long CreditNoteId { get; set; }
    public long TenantId { get; set; } = 1;
    public long? InvoiceId { get; set; }
    public long? OrderId { get; set; }
    public string CreditNoteNumber { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; }

    public ICollection<CreditNoteItem> Items { get; set; } = new List<CreditNoteItem>();
}
