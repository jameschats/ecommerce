namespace ecomm.api.Features.Checkout;

/// <summary>Resolved GST for a single taxable amount.</summary>
public sealed record TaxLine(decimal Rate, decimal TaxAmount, decimal Cgst, decimal Sgst, decimal Igst, bool InterState);

/// <summary>Shipping serviceability + charge for a destination pincode.</summary>
public sealed record ShippingQuote(bool Serviceable, long? MethodId, string MethodName, decimal Charge, int? EstimatedDays, string? Message);
