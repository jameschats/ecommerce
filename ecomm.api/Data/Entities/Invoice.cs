namespace ecomm.api.Data.Entities;

public class Invoice
{
    public long InvoiceId { get; set; }
    public long TenantId { get; set; } = 1;
    public long OrderId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string? BillingName { get; set; }
    public string? BuyerCompanyName { get; set; }
    public string? BillingAddress { get; set; }
    public string? ShippingAddress { get; set; }

    /// <summary>The SELLER's GSTIN. Left null on a Bill of Supply, which charges no GST.</summary>
    public string? GstNumber { get; set; }

    /// <summary>The BUYER's GSTIN, snapshotted so a later edit cannot alter an issued document.</summary>
    public string? BuyerGstin { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? PdfUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}
