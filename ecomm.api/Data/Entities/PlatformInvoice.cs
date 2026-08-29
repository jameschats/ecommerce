namespace ecomm.api.Data.Entities;

/// <summary>
/// A GST tax invoice the platform issues to a merchant for one subscription charge. GLOBAL (not
/// ITenantScoped): the invoice-number series is per the platform's GSTIN across ALL merchants, so
/// numbering must be gap-free per financial year platform-wide. <see cref="TenantId"/> is a plain FK to
/// the billed store. One invoice per <see cref="TenantBillingHistoryId"/> (idempotent).
/// </summary>
public class PlatformInvoice
{
    public long PlatformInvoiceId { get; set; }
    public long TenantId { get; set; }
    public long TenantBillingHistoryId { get; set; }
    /// <summary>Invoice | CreditNote (C2/C3). A credit note reverses (part of) an invoice on refund.</summary>
    public string DocumentType { get; set; } = "Invoice";
    /// <summary>For a credit note: the invoice it reverses.</summary>
    public long? OriginalInvoiceId { get; set; }
    public string? Notes { get; set; }

    /// <summary>Indian financial year, e.g. "2026-27".</summary>
    public string FinancialYear { get; set; } = string.Empty;
    /// <summary>Per-FY sequence (global), gap-free.</summary>
    public int SequenceNumber { get; set; }
    /// <summary>Formatted number shown on the invoice, e.g. "WAV/2026-27/0001".</summary>
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }

    // Seller (platform) snapshot at issue time.
    public string? SellerName { get; set; }
    public string? SellerGstin { get; set; }
    public string? SellerState { get; set; }

    // Buyer (merchant) snapshot at issue time.
    public string? BuyerName { get; set; }
    public string? BuyerGstin { get; set; }
    public string? BuyerState { get; set; }

    public string PlaceOfSupply { get; set; } = string.Empty;
    public bool IsInterState { get; set; }
    public decimal GstRatePercent { get; set; }

    // Amounts (prices are GST-inclusive; taxable value is back-calculated).
    public decimal TaxableValue { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }
}
