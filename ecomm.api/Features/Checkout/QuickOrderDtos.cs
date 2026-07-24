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

/// <summary>Everything the order form needs to render before the buyer types anything.</summary>
public sealed record QuickOrderConfigDto(
    decimal DefaultMinOrderAmount,
    decimal PackingChargePct,
    IReadOnlyList<StateMinOrderDto> StateMinOrders,
    IReadOnlyList<string> States,
    string? AnnouncementText,
    string? PriceValidUpto);
