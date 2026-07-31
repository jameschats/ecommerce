namespace ecomm.api.Features.Orders;

// ----- Checkout preview -----
public sealed record CheckoutQuoteLine(
    long productId, long? productVariantId, string name, string? variantLabel,
    int quantity, decimal unitPrice, decimal lineSubtotal, decimal taxRate, decimal taxAmount, int availableQty, bool inStock);

public sealed record CheckoutQuoteDto(
    bool serviceable, string? message,
    IReadOnlyList<CheckoutQuoteLine> lines,
    decimal subtotal, decimal taxAmount, decimal cgst, decimal sgst, decimal igst, bool interState,
    decimal shippingCharge, string shippingMethod, int? estimatedDays,
    decimal total, long? shippingAddressId, string taxMode,
    decimal discountAmount, string? couponCode, string? couponMessage, bool couponApplied,
    bool codEnabled, string? giftProductName);

// ----- Place / pay -----
public sealed record PlaceOrderRequest(long ShippingAddressId, long? BillingAddressId, string? Notes, string? CouponCode, string? PaymentMethod);
public sealed record ConfirmPaymentRequest(string GatewayPaymentId, string Signature);
public sealed record CancelOrderRequest(string? Reason);

// ----- Shipments -----
public sealed record CreateShipmentRequest(string Courier, string TrackingNumber, DateTime? EstimatedDeliveryDate);
public sealed record ShipmentDto(long shipmentId, string? courier, string? trackingNumber, string status,
    DateTime? estimatedDeliveryDate, DateTime? shippedAt, DateTime? deliveredAt);

public sealed record PaymentInit(string gateway, string? publicKey, string gatewayOrderId, long paymentId, decimal amount, string currency);
public sealed record PlaceOrderResult(long orderId, string orderNumber, decimal amount, string currency, PaymentInit? payment, bool codOrder);

// ----- Read models -----
public sealed record OrderAddressDto(
    string? recipientName, string? phone, string line1, string? line2, string city, string state, string pincode, string country);

public sealed record OrderItemDto(
    long orderItemId, long productId, string productName, string? sku, string? slug, string? variantLabel,
    string? hsnCode, int quantity, decimal unitPrice, decimal taxRate, decimal taxAmount, decimal lineTotal, bool isFreeGift);

/// <summary>One step in the order's journey — our own status changes plus courier scans, merged.</summary>
public sealed record OrderTimelineEntryDto(string status, string? note, string? location, DateTime at, string source);

public sealed record OrderDto(
    long orderId, string orderNumber, string status, string currency,
    decimal subtotal, decimal discountAmount, decimal taxAmount, decimal shippingAmount, decimal totalAmount,
    DateTime? placedAt, DateTime createdAt,
    IReadOnlyList<OrderItemDto> items,
    OrderAddressDto? shippingAddress, OrderAddressDto? billingAddress,
    string? paymentMethod, string? paymentStatus,
    long? invoiceId, string? invoiceNumber,
    bool canCancel,
    ShipmentDto? shipment,
    IReadOnlyList<OrderTimelineEntryDto> timeline);

public sealed record OrderListItem(
    long orderId, string orderNumber, string status, decimal totalAmount, int itemCount,
    string? firstItemName, string? firstItemImage, DateTime? placedAt, DateTime createdAt,
    bool isTest);
