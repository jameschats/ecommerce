namespace ecomm.api.Features.Checkout;

public static class TaxMode
{
    public const string Exclusive = "Exclusive";   // GST added on top
    public const string Inclusive = "Inclusive";    // price includes GST (reverse-calculated)
    public const string None = "None";              // no GST (Bill of Supply)
}

/// <summary>GST computed for one line: Net (taxable) + Tax split. In Inclusive mode
/// Net + Tax = the listed amount; in Exclusive mode Net = listed and Tax is on top.</summary>
public sealed record TaxLineResult(decimal Rate, decimal Net, decimal Tax, decimal Cgst, decimal Sgst, decimal Igst);

/// <summary>Shipping serviceability + charge for a destination pincode.</summary>
public sealed record ShippingQuote(bool Serviceable, long? MethodId, string MethodName, decimal Charge, int? EstimatedDays, string? Message);
