namespace ecomm.api.Data.Entities;

/// <summary>
/// The platform's own seller details for the GST tax invoices it issues to merchants for their
/// subscription fees. Single global row (like PlatformEmailSettings), editable in super-admin. Distinct
/// from a merchant's StoreGstin/StoreLegalName (which are for the merchant's own storefront invoices).
/// </summary>
public class PlatformBillingSettings
{
    public int PlatformBillingSettingsId { get; set; }
    public string? SellerLegalName { get; set; }
    public string? SellerGstin { get; set; }
    public string? SellerAddress { get; set; }
    /// <summary>Seller's state — the place-of-supply origin that decides CGST/SGST (intra) vs IGST (inter).</summary>
    public string? SellerState { get; set; }
    /// <summary>GST rate on SaaS (18% in India). Prices are treated as GST-inclusive.</summary>
    public decimal GstRatePercent { get; set; } = 18m;
    /// <summary>Invoice-number prefix, e.g. "WAV" → WAV/2026-27/0001.</summary>
    public string InvoicePrefix { get; set; } = "INV";
    public DateTime? UpdatedAt { get; set; }
}
