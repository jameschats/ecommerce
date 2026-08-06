namespace ecomm.api.Features.Checkout;

/// <summary>One line as the browser believes it to be. Only the ids and quantities are
/// trusted — every price is re-read from the database (see <c>QuickOrderService</c>).</summary>
public sealed record QuickOrderLineRequest(long ProductId, int Quantity);

public sealed record QuickOrderQuoteRequest(
    IReadOnlyList<QuickOrderLineRequest> Lines,
    string? State);

/// <summary>A priced line, with the server's numbers rather than the browser's.</summary>
public sealed record QuickOrderQuoteLineDto(
    long ProductId, string Sku, string Name, int Quantity,
    decimal UnitPrice, decimal? CompareAtPrice, decimal LineTotal, bool InStock);

/// <summary>
/// The authoritative order summary. Mirrors the reference layout: Net Total, Discount
/// Total, Sub Total, Min. Order Amount, Packing Charges, Round Off, Overall Amount.
/// </summary>
public sealed record QuickOrderQuoteDto(
    IReadOnlyList<QuickOrderQuoteLineDto> Lines,
    int ItemCount,
    int TotalUnits,
    decimal NetTotal,
    decimal DiscountTotal,
    decimal SubTotal,
    decimal MinOrderAmount,
    decimal PackingChargePct,
    decimal PackingCharges,
    decimal RoundOff,
    decimal OverallAmount,
    bool MeetsMinimum,
    IReadOnlyList<string> Warnings);

public sealed record StateMinOrderDto(string StateName, decimal MinOrderAmount);

/// <summary>Delivery details captured on the order form, plus the basket.</summary>
/// <summary>A basket to be rendered as a printable quotation.</summary>
/// <param name="CustomerName">Who the quote is for. Optional — it prints as "Prepared for".</param>
public sealed record QuoteDocumentRequest(
    IReadOnlyList<QuickOrderLineRequest> Lines, string? State, string? CustomerName);

/// <param name="BusinessName">Trading name when ordering for a shop. Optional.</param>
/// <param name="Gstin">The buyer's GST number, printed on their bill. Optional.</param>
/// <param name="ShipToDifferent">
/// When false — the common case — the delivery address is the billing address and every
/// Ship* field is ignored. Defaulted so existing callers and the old payload still work.
/// </param>
public sealed record PlaceQuickOrderRequest(
    IReadOnlyList<QuickOrderLineRequest> Lines,
    string State, string? City, string Name, string Mobile, string? Email, string Address,
    string? BusinessName = null,
    string? Gstin = null,
    bool ShipToDifferent = false,
    string? ShipName = null,
    string? ShipMobile = null,
    string? ShipAddress = null,
    string? ShipCity = null,
    string? ShipState = null);

public sealed record PlaceQuickOrderResult(
    long OrderId, string OrderNumber, decimal OverallAmount, string Status);

/// <summary>Everything the order form needs to render before the buyer types anything.</summary>
public sealed record QuickOrderConfigDto(
    decimal DefaultMinOrderAmount,
    decimal PackingChargePct,
    IReadOnlyList<StateMinOrderDto> StateMinOrders,
    IReadOnlyList<string> States,
    string? AnnouncementText,
    string? PriceValidUpto);
