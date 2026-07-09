using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Checkout;
using ecomm.api.Features.Coupons;
using ecomm.api.Features.Inventory;
using ecomm.api.Features.Notifications;
using ecomm.api.Features.Payments;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Orders;

public interface IOrderService
{
    Task<CheckoutQuoteDto> QuoteAsync(long userId, long? shippingAddressId, string? couponCode, CancellationToken ct = default);
    Task<PlaceOrderResult> PlaceOrderAsync(long userId, PlaceOrderRequest req, CancellationToken ct = default);
    Task<OrderDto> ConfirmPaymentAsync(long userId, long orderId, ConfirmPaymentRequest req, CancellationToken ct = default);
    Task<OrderDto> CancelOrderAsync(long userId, long orderId, CancelOrderRequest req, bool isAdmin, CancellationToken ct = default);
    Task<List<OrderListItem>> ListMineAsync(long userId, CancellationToken ct = default);
    Task<OrderDto?> GetAsync(long orderId, long? userId, bool isAdmin, CancellationToken ct = default);
    Task<PagedResult<OrderListItem>> ListAllAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<OrderDto?> UpdateStatusAsync(long orderId, string toStatus, long? userId, CancellationToken ct = default);
    Task<OrderDto?> CreateShipmentAsync(long orderId, CreateShipmentRequest req, long? userId, CancellationToken ct = default);
    Task<OrderDto?> MarkDeliveredAsync(long orderId, long? userId, CancellationToken ct = default);
}

public sealed class OrderService : IOrderService
{
    private long Tenant => _db.CurrentTenantId;

    private readonly EcommerceDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly ITaxService _tax;
    private readonly IShippingService _shipping;
    private readonly IPaymentGateway _gateway;
    private readonly IInvoiceService _invoices;
    private readonly INotificationService _notify;
    private readonly INotificationFeedService _feed;
    private readonly ICouponService _coupons;
    private readonly ILogger<OrderService> _log;

    public OrderService(EcommerceDbContext db, IInventoryService inventory, ITaxService tax,
        IShippingService shipping, IPaymentGateway gateway, IInvoiceService invoices,
        INotificationService notify, INotificationFeedService feed, ICouponService coupons, ILogger<OrderService> log)
    {
        _db = db; _inventory = inventory; _tax = tax; _shipping = shipping;
        _gateway = gateway; _invoices = invoices; _notify = notify; _feed = feed; _coupons = coupons; _log = log;
    }

    /// <summary>Fire order lifecycle notifications (email always; SMS when smsCode given).
    /// Never throws — notification failure must not break the order flow.</summary>
    private async Task NotifyOrderAsync(long orderId, string emailCode, string? smsCode,
        IReadOnlyDictionary<string, string>? extra, CancellationToken ct)
    {
        try
        {
            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
            if (order is null) return;
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == order.UserId, ct);
            if (user is null) return;
            var storeName = await _db.Settings.Where(s => s.TenantId == Tenant && s.SettingKey == "SiteName")
                .Select(s => s.SettingValue).FirstOrDefaultAsync(ct) ?? "CalendarShop";

            var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerName"] = user.FullName ?? "there",
                ["OrderNumber"] = order.OrderNumber,
                ["OrderTotal"] = $"{order.Currency} {order.TotalAmount:0.00}",
                ["Status"] = order.Status,
                ["StoreName"] = storeName,
            };
            if (extra is not null) foreach (var kv in extra) tokens[kv.Key] = kv.Value;

            if (!string.IsNullOrWhiteSpace(user.Email)) await _notify.SendEmailAsync(emailCode, user.Email!, tokens, ct);
            if (smsCode is not null && !string.IsNullOrWhiteSpace(user.PhoneNumber)) await _notify.SendSmsAsync(smsCode, user.PhoneNumber!, tokens, ct);

            // In-app bell: notify the customer, and (on confirmation) the admins of a new order.
            var feedTitle = emailCode switch
            {
                "OrderConfirmation" => "Order confirmed",
                "OrderShipped" => "Order shipped",
                "OrderCancelled" => "Order cancelled",
                _ => $"Order {order.Status}",
            };
            await _feed.NotifyUserAsync(order.UserId, "OrderUpdate", feedTitle, $"Order {order.OrderNumber}", $"/account/orders/{orderId}", ct);
            if (emailCode == "OrderConfirmation")
                await _feed.NotifyAdminsAsync("NewOrder", "New order received",
                    $"Order {order.OrderNumber} · {order.Currency} {order.TotalAmount:0.00}", "/admin/orders", ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Order notification '{Code}' failed for order {OrderId}", emailCode, orderId);
        }
    }

    // ---------------- Quote ----------------
    public async Task<CheckoutQuoteDto> QuoteAsync(long userId, long? shippingAddressId, string? couponCode, CancellationToken ct = default)
    {
        var lines = await LoadCartLinesAsync(userId, ct);
        var address = await ResolveAddressAsync(userId, shippingAddressId, ct);
        var mode = await _tax.GetTaxModeAsync(ct);
        var interState = await _tax.IsInterStateAsync(address?.State, ct);

        var quoteLines = new List<CheckoutQuoteLine>();
        decimal subtotal = 0m, taxTotal = 0m, cgst = 0m, sgst = 0m, igst = 0m;
        foreach (var l in lines)
        {
            var listed = l.UnitPrice * l.Quantity;
            var rate = await _tax.ResolveRateAsync(l.Hsn, ct);
            var r = _tax.ComputeLine(listed, rate, interState, mode);
            subtotal += listed; taxTotal += r.Tax; cgst += r.Cgst; sgst += r.Sgst; igst += r.Igst;
            quoteLines.Add(new CheckoutQuoteLine(l.ProductId, l.VariantId, l.Name, l.VariantLabel,
                l.Quantity, l.UnitPrice, listed, r.Rate, r.Tax, l.Available, l.Available >= l.Quantity));
        }

        var ship = await _shipping.QuoteAsync(address?.Pincode, subtotal, ct);
        var serviceable = address is not null && ship.Serviceable;
        var message = address is null ? "Select a delivery address." : ship.Message;
        var charge = serviceable ? ship.Charge : 0m;

        // Discount: a typed code, else the best automatic offer. Line-aware so product/collection targeting works.
        var discountLines = lines.Select(l => new DiscountLine(l.ProductId, l.UnitPrice * l.Quantity)).ToList();
        var coupon = await _coupons.EvaluateAsync(couponCode, userId, discountLines, ct);
        if (coupon.Ok && coupon.FreeShipping) charge = 0m;   // free-shipping offer

        // Exclusive adds tax on top; Inclusive/None already have it in the listed price.
        var total = subtotal + (mode == TaxMode.Exclusive ? taxTotal : 0m) + charge - coupon.Discount;
        if (total < 0m) total = 0m;

        return new CheckoutQuoteDto(serviceable, message, quoteLines,
            subtotal, taxTotal, cgst, sgst, igst, interState,
            charge, ship.MethodName, ship.EstimatedDays,
            total, address?.CustomerAddressId, mode,
            coupon.Discount, coupon.Code ?? (string.IsNullOrWhiteSpace(couponCode) ? null : couponCode.Trim()),
            coupon.Ok ? coupon.Description : coupon.Error, coupon.Ok,
            await CodEnabledAsync(ct));
    }

    private async Task<bool> CodEnabledAsync(CancellationToken ct) =>
        await _db.Settings.Where(s => s.TenantId == Tenant && s.SettingKey == "CodEnabled")
            .Select(s => s.SettingValue).FirstOrDefaultAsync(ct) == "true";

    // ---------------- Place ----------------
    public async Task<PlaceOrderResult> PlaceOrderAsync(long userId, PlaceOrderRequest req, CancellationToken ct = default)
    {
        var shipAddr = await _db.CustomerAddresses.FirstOrDefaultAsync(
            a => a.CustomerAddressId == req.ShippingAddressId && a.UserId == userId && !a.IsDeleted, ct)
            ?? throw new AppException("Shipping address not found.", 404);
        long? billingId = req.BillingAddressId;
        if (billingId is { } bid && !await _db.CustomerAddresses.AnyAsync(a => a.CustomerAddressId == bid && a.UserId == userId && !a.IsDeleted, ct))
            throw new AppException("Billing address not found.", 404);

        var cart = await _db.Carts.FirstOrDefaultAsync(c => c.UserId == userId && c.Status == "Active" && c.TenantId == Tenant, ct)
            ?? throw new AppException("Your cart is empty.");
        var lines = await LoadCartLinesAsync(userId, ct);
        if (lines.Count == 0) throw new AppException("Your cart is empty.");

        var mode = await _tax.GetTaxModeAsync(ct);
        var interState = await _tax.IsInterStateAsync(shipAddr.State, ct);
        decimal subtotal = 0m, taxTotal = 0m;
        foreach (var l in lines)
        {
            var rate = await _tax.ResolveRateAsync(l.Hsn, ct);
            l.LineSub = l.UnitPrice * l.Quantity;
            var r = _tax.ComputeLine(l.LineSub, rate, interState, mode);
            l.Rate = r.Rate;          // effective rate (0 in None mode)
            l.LineTax = r.Tax;
            subtotal += l.LineSub; taxTotal += l.LineTax;
        }
        var ship = await _shipping.QuoteAsync(shipAddr.Pincode, subtotal, ct);
        if (!ship.Serviceable) throw new AppException(ship.Message ?? "This address is not serviceable.");

        // Discount: re-validate server-side (never trust a client-computed discount). A typed code that
        // fails is an error; a blank code still applies the best automatic offer. Line-aware for targeting.
        var discountLines = lines.Select(l => new DiscountLine(l.ProductId, l.LineSub)).ToList();
        var coupon = await _coupons.EvaluateAsync(req.CouponCode, userId, discountLines, ct);
        if (!string.IsNullOrWhiteSpace(req.CouponCode) && !coupon.Ok)
            throw new AppException(coupon.Error ?? "That coupon can't be applied.");
        var discount = coupon.Discount;
        var shippingCharge = coupon is { Ok: true, FreeShipping: true } ? 0m : ship.Charge;
        var total = subtotal + (mode == TaxMode.Exclusive ? taxTotal : 0m) + shippingCharge - discount;
        if (total < 0m) total = 0m;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var order = new Order
            {
                TenantId = Tenant,
                UserId = userId,
                OrderNumber = $"TMP-{Guid.NewGuid():N}".Substring(0, 18),
                Status = "Pending",
                BillingAddressId = billingId ?? shipAddr.CustomerAddressId,
                ShippingAddressId = shipAddr.CustomerAddressId,
                ShippingMethodId = ship.MethodId,
                Currency = "INR",
                CouponId = coupon.Ok ? coupon.CouponId : null,
                Subtotal = subtotal,
                DiscountAmount = discount,
                TaxAmount = taxTotal,
                ShippingAmount = shippingCharge,
                TotalAmount = total,
                Notes = req.Notes,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync(ct);
            order.OrderNumber = $"ORD{DateTime.UtcNow:yyyyMMdd}-{order.OrderId:D5}";

            foreach (var l in lines)
            {
                var ok = await _inventory.ReserveAsync(l.ProductId, l.VariantId, l.Quantity, "Order", order.OrderId, ct);
                if (!ok) throw new AppException($"'{l.Name}' is out of stock.");
                _db.OrderItems.Add(new OrderItem
                {
                    OrderId = order.OrderId,
                    ProductId = l.ProductId,
                    ProductVariantId = l.VariantId,
                    Sku = l.Sku,
                    ProductName = l.Name,
                    HsnCode = l.Hsn,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    UnitCost = l.Cost,
                    DiscountAmount = 0m,
                    TaxRate = l.Rate,
                    TaxAmount = l.LineTax,
                    LineTotal = l.LineSub,
                    CreatedAt = DateTime.UtcNow,
                });
            }

            var isCod = string.Equals(req.PaymentMethod, "COD", StringComparison.OrdinalIgnoreCase);
            if (isCod && !await CodEnabledAsync(ct))
                throw new AppException("Cash on delivery isn't available right now.");

            PlaceOrderResult result;
            if (isCod)
            {
                // COD: no prepayment. Confirm the order + commit inventory now (the sale is accepted);
                // cash is collected on delivery. No gateway, no payment widget.
                foreach (var l in lines)
                    await _inventory.CommitAsync(l.ProductId, l.VariantId, l.Quantity, "Order", order.OrderId, ct);

                _db.Payments.Add(new Payment
                {
                    TenantId = Tenant, OrderId = order.OrderId, Method = "COD",
                    Status = "Pending", Amount = total, Currency = "INR", CreatedAt = DateTime.UtcNow,
                });
                order.Status = "Confirmed";
                order.PlacedAt = DateTime.UtcNow;
                _db.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.OrderId, FromStatus = null, ToStatus = "Confirmed", Notes = "COD order placed", ChangedBy = userId, CreatedAt = DateTime.UtcNow,
                });
                result = new PlaceOrderResult(order.OrderId, order.OrderNumber, total, "INR", null, true);
            }
            else
            {
                var gatewayOrder = await _gateway.CreateOrderAsync(order.OrderId, total, "INR", order.OrderNumber, ct);
                var payment = new Payment
                {
                    TenantId = Tenant, OrderId = order.OrderId, Method = _gateway.Name,
                    Status = "Pending", Amount = total, Currency = "INR", CreatedAt = DateTime.UtcNow,
                };
                _db.Payments.Add(payment);
                await _db.SaveChangesAsync(ct);

                _db.PaymentTransactions.Add(new PaymentTransaction
                {
                    PaymentId = payment.PaymentId, Gateway = _gateway.Name, GatewayOrderId = gatewayOrder.GatewayOrderId,
                    TransactionType = "Authorize", Amount = total, Status = "Created", CreatedAt = DateTime.UtcNow,
                });
                _db.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.OrderId, FromStatus = null, ToStatus = "Pending", Notes = "Order placed", ChangedBy = userId, CreatedAt = DateTime.UtcNow,
                });
                result = new PlaceOrderResult(order.OrderId, order.OrderNumber, total, "INR",
                    new PaymentInit(_gateway.Name, _gateway.PublicKey, gatewayOrder.GatewayOrderId, payment.PaymentId, total, "INR"), false);
            }

            cart.Status = "Converted";
            cart.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            if (coupon.Ok && coupon.CouponId is { } couponId)
                await _coupons.RecordUsageAsync(couponId, userId, order.OrderId, discount, ct);

            await tx.CommitAsync(ct);

            // COD orders are confirmed at placement → send the confirmation now (online sends on payment capture).
            if (isCod) await NotifyOrderAsync(order.OrderId, "OrderConfirmation", "OrderConfirmation", null, ct);

            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ---------------- Confirm payment ----------------
    public async Task<OrderDto> ConfirmPaymentAsync(long userId, long orderId, ConfirmPaymentRequest req, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId, ct)
            ?? throw new AppException("Order not found.", 404);
        if (order.Status != "Pending") throw new AppException("This order has already been processed.");

        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, ct)
            ?? throw new AppException("Payment not found.", 404);
        var auth = await _db.PaymentTransactions
            .Where(t => t.PaymentId == payment.PaymentId && t.TransactionType == "Authorize")
            .OrderByDescending(t => t.PaymentTransactionId).FirstOrDefaultAsync(ct)
            ?? throw new AppException("Payment session not found.", 404);

        var valid = _gateway.VerifySignature(auth.GatewayOrderId ?? "", req.GatewayPaymentId, req.Signature);
        if (!valid)
        {
            payment.Status = "Failed";
            payment.UpdatedAt = DateTime.UtcNow;
            _db.PaymentTransactions.Add(new PaymentTransaction
            {
                PaymentId = payment.PaymentId, Gateway = _gateway.Name, GatewayOrderId = auth.GatewayOrderId,
                GatewayPaymentId = req.GatewayPaymentId, GatewaySignature = req.Signature,
                TransactionType = "Capture", Amount = payment.Amount, Status = "Failed", CreatedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
            throw new AppException("Payment verification failed.", 402);
        }

        payment.Status = "Success";
        payment.UpdatedAt = DateTime.UtcNow;
        _db.PaymentTransactions.Add(new PaymentTransaction
        {
            PaymentId = payment.PaymentId, Gateway = _gateway.Name, GatewayOrderId = auth.GatewayOrderId,
            GatewayPaymentId = req.GatewayPaymentId, GatewaySignature = req.Signature,
            TransactionType = "Capture", Amount = payment.Amount, Status = "Captured", CreatedAt = DateTime.UtcNow,
        });

        order.Status = "Paid";
        order.PlacedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId, FromStatus = "Pending", ToStatus = "Paid", Notes = "Payment captured", ChangedBy = userId, CreatedAt = DateTime.UtcNow,
        });

        var items = await _db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync(ct);
        foreach (var it in items)
            await _inventory.CommitAsync(it.ProductId, it.ProductVariantId, it.Quantity, "Order", orderId, ct);

        await _db.SaveChangesAsync(ct);

        try { await _invoices.GenerateForOrderAsync(orderId, ct); }
        catch (Exception ex) { _log.LogError(ex, "Invoice generation failed for order {OrderId}", orderId); }

        await NotifyOrderAsync(orderId, "OrderConfirmation", "OrderConfirmation", null, ct);

        return (await GetAsync(orderId, userId, false, ct))!;
    }

    // ---------------- Cancel ----------------
    public async Task<OrderDto> CancelOrderAsync(long userId, long orderId, CancelOrderRequest req, bool isAdmin, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && (isAdmin || o.UserId == userId), ct)
            ?? throw new AppException("Order not found.", 404);
        if (order.Status is "Shipped" or "Delivered" or "Cancelled" or "Returned")
            throw new AppException($"An order that is {order.Status} cannot be cancelled.");

        var items = await _db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync(ct);
        // Inventory is committed once an order is Paid/Confirmed (COD)/Packed; only Pending orders are still reserved.
        var wasCommitted = order.Status is "Paid" or "Packed" or "Confirmed";

        foreach (var it in items)
        {
            if (wasCommitted) await _inventory.RestockAsync(it.ProductId, it.ProductVariantId, it.Quantity, "Order", orderId, ct);
            else await _inventory.ReleaseAsync(it.ProductId, it.ProductVariantId, it.Quantity, "Order", orderId, ct);
        }

        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);
        if (payment is not null && payment.Status == "Success")
        {
            var capture = await _db.PaymentTransactions
                .Where(t => t.PaymentId == payment.PaymentId && t.TransactionType == "Capture" && t.Status == "Captured")
                .OrderByDescending(t => t.PaymentTransactionId).FirstOrDefaultAsync(ct);
            var refundRes = await _gateway.RefundAsync(capture?.GatewayPaymentId ?? "", payment.Amount, ct);
            _db.Refunds.Add(new Refund
            {
                PaymentId = payment.PaymentId, OrderId = orderId, Amount = payment.Amount,
                Reason = req.Reason ?? "Order cancelled", Status = "Processed",
                GatewayRefundId = refundRes.GatewayRefundId, ProcessedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
            });
            _db.PaymentTransactions.Add(new PaymentTransaction
            {
                PaymentId = payment.PaymentId, Gateway = _gateway.Name, GatewayPaymentId = capture?.GatewayPaymentId,
                TransactionType = "Refund", Amount = payment.Amount, Status = "Processed",
                RawResponse = refundRes.GatewayRefundId, CreatedAt = DateTime.UtcNow,
            });
            payment.Status = "Refunded";
            payment.UpdatedAt = DateTime.UtcNow;
        }
        else if (payment is not null)
        {
            payment.Status = "Failed";
            payment.UpdatedAt = DateTime.UtcNow;
        }

        var fromStatus = order.Status;
        order.Status = "Cancelled";
        order.UpdatedAt = DateTime.UtcNow;
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId, FromStatus = fromStatus, ToStatus = "Cancelled",
            Notes = req.Reason ?? "Cancelled", ChangedBy = userId, CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        await NotifyOrderAsync(orderId, "OrderCancelled", "OrderCancelled", null, ct);
        return (await GetAsync(orderId, userId, isAdmin, ct))!;
    }

    // ---------------- Reads ----------------
    public Task<List<OrderListItem>> ListMineAsync(long userId, CancellationToken ct = default) =>
        _db.Orders.Where(o => o.UserId == userId && o.TenantId == Tenant && o.Status != "Draft")
            .OrderByDescending(o => o.OrderId)
            .Select(o => new OrderListItem(
                o.OrderId, o.OrderNumber, o.Status, o.TotalAmount,
                _db.OrderItems.Count(i => i.OrderId == o.OrderId),
                _db.OrderItems.Where(i => i.OrderId == o.OrderId).OrderBy(i => i.OrderItemId).Select(i => i.ProductName).FirstOrDefault(),
                _db.OrderItems.Where(i => i.OrderId == o.OrderId).OrderBy(i => i.OrderItemId)
                    .Select(i => _db.ProductImages.Where(im => im.ProductId == i.ProductId).OrderByDescending(im => im.IsPrimary).Select(im => im.Url).FirstOrDefault()).FirstOrDefault(),
                o.PlacedAt, o.CreatedAt))
            .ToListAsync(ct);

    public async Task<OrderDto?> GetAsync(long orderId, long? userId, bool isAdmin, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && (isAdmin || o.UserId == userId), ct);
        if (order is null) return null;

        var items = await _db.OrderItems.Where(i => i.OrderId == orderId).OrderBy(i => i.OrderItemId)
            .Select(i => new OrderItemDto(
                i.OrderItemId, i.ProductId, i.ProductName, i.Sku,
                _db.Products.Where(p => p.ProductId == i.ProductId).Select(p => p.Slug).FirstOrDefault(),
                i.ProductVariantId == null ? null : _db.ProductVariants.Where(v => v.ProductVariantId == i.ProductVariantId).Select(v => v.Name).FirstOrDefault(),
                i.HsnCode, i.Quantity, i.UnitPrice, i.TaxRate, i.TaxAmount, i.LineTotal))
            .ToListAsync(ct);

        var ship = await AddressDtoAsync(order.ShippingAddressId, ct);
        var bill = await AddressDtoAsync(order.BillingAddressId, ct);
        var payment = await _db.Payments.Where(p => p.OrderId == orderId).OrderByDescending(p => p.PaymentId)
            .Select(p => new { p.Method, p.Status }).FirstOrDefaultAsync(ct);
        var invoice = await _db.Invoices.Where(i => i.OrderId == orderId)
            .Select(i => new { i.InvoiceId, i.InvoiceNumber }).FirstOrDefaultAsync(ct);

        var canCancel = order.Status is "Pending" or "Paid" or "Packed" or "Confirmed";

        var shipment = await _db.Shipments.Where(s => s.OrderId == orderId).OrderByDescending(s => s.ShipmentId)
            .Select(s => new ShipmentDto(s.ShipmentId, s.Courier, s.TrackingNumber, s.Status,
                s.EstimatedDeliveryDate, s.ShippedAt, s.DeliveredAt))
            .FirstOrDefaultAsync(ct);

        return new OrderDto(order.OrderId, order.OrderNumber, order.Status, order.Currency,
            order.Subtotal, order.DiscountAmount, order.TaxAmount, order.ShippingAmount, order.TotalAmount,
            order.PlacedAt, order.CreatedAt, items, ship, bill,
            payment?.Method, payment?.Status, invoice?.InvoiceId, invoice?.InvoiceNumber, canCancel, shipment);
    }

    public async Task<PagedResult<OrderListItem>> ListAllAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Orders.Where(o => o.TenantId == Tenant && o.Status != "Draft");   // drafts live in their own screen
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(o => o.Status == status);
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(o => o.OrderId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrderListItem(
                o.OrderId, o.OrderNumber, o.Status, o.TotalAmount,
                _db.OrderItems.Count(i => i.OrderId == o.OrderId),
                _db.OrderItems.Where(i => i.OrderId == o.OrderId).OrderBy(i => i.OrderItemId).Select(i => i.ProductName).FirstOrDefault(),
                null, o.PlacedAt, o.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<OrderListItem> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    // Valid single-step admin transitions (Confirmed = COD start, Paid = online start).
    private static readonly Dictionary<string, string> NextAdminStatus = new()
    {
        ["Paid"] = "Packed", ["Confirmed"] = "Packed", ["Packed"] = "Shipped", ["Shipped"] = "Delivered",
    };

    public async Task<OrderDto?> UpdateStatusAsync(long orderId, string toStatus, long? userId, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return null;
        if (!NextAdminStatus.TryGetValue(order.Status, out var expected) || expected != toStatus)
            throw new AppException($"Cannot move an order from {order.Status} to {toStatus}.");

        var from = order.Status;
        order.Status = toStatus;
        order.UpdatedAt = DateTime.UtcNow;
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId, FromStatus = from, ToStatus = toStatus, Notes = "Status updated by admin", ChangedBy = userId, CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        if (toStatus == "Delivered") await CollectCodOnDeliveryAsync(orderId, ct);
        // Generic status email (Shipped-with-tracking is sent by the Shipments feature).
        await NotifyOrderAsync(orderId, "OrderStatusUpdate", null, null, ct);
        return await GetAsync(orderId, null, true, ct);
    }

    /// <summary>On delivery of a COD order, record the cash as collected (payment → Success).</summary>
    private async Task CollectCodOnDeliveryAsync(long orderId, CancellationToken ct)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId && p.Method == "COD" && p.Status == "Pending", ct);
        if (payment is null) return;
        payment.Status = "Success";
        payment.UpdatedAt = DateTime.UtcNow;
        _db.PaymentTransactions.Add(new PaymentTransaction
        {
            PaymentId = payment.PaymentId, Gateway = "COD", TransactionType = "Capture",
            Amount = payment.Amount, Status = "Captured", CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Shipments ----------------
    public async Task<OrderDto?> CreateShipmentAsync(long orderId, CreateShipmentRequest req, long? userId, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.TenantId == Tenant, ct);
        if (order is null) return null;
        if (order.Status is not ("Paid" or "Packed" or "Confirmed"))
            throw new AppException($"An order that is {order.Status} can't be shipped.");
        if (string.IsNullOrWhiteSpace(req.Courier) || string.IsNullOrWhiteSpace(req.TrackingNumber))
            throw new AppException("Courier and tracking number are required.");

        var now = DateTime.UtcNow;
        _db.Shipments.Add(new Shipment
        {
            TenantId = Tenant, OrderId = orderId, ShippingMethodId = order.ShippingMethodId,
            Courier = req.Courier.Trim(), TrackingNumber = req.TrackingNumber.Trim(),
            Status = "Shipped", EstimatedDeliveryDate = req.EstimatedDeliveryDate, ShippedAt = now, CreatedAt = now,
        });
        var from = order.Status;
        order.Status = "Shipped";
        order.UpdatedAt = now;
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId, FromStatus = from, ToStatus = "Shipped",
            Notes = $"Shipped via {req.Courier.Trim()} ({req.TrackingNumber.Trim()})", ChangedBy = userId, CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);

        await NotifyOrderAsync(orderId, "OrderShipped", "OrderShipped",
            new Dictionary<string, string> { ["Courier"] = req.Courier.Trim(), ["TrackingNumber"] = req.TrackingNumber.Trim() }, ct);

        return await GetAsync(orderId, null, true, ct);
    }

    public async Task<OrderDto?> MarkDeliveredAsync(long orderId, long? userId, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.TenantId == Tenant, ct);
        if (order is null) return null;
        if (order.Status != "Shipped") throw new AppException($"An order that is {order.Status} can't be marked delivered.");

        var now = DateTime.UtcNow;
        var shipment = await _db.Shipments.Where(s => s.OrderId == orderId).OrderByDescending(s => s.ShipmentId).FirstOrDefaultAsync(ct);
        if (shipment is not null) { shipment.Status = "Delivered"; shipment.DeliveredAt = now; shipment.UpdatedAt = now; }
        order.Status = "Delivered";
        order.UpdatedAt = now;
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId, FromStatus = "Shipped", ToStatus = "Delivered", Notes = "Delivered", ChangedBy = userId, CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);
        await CollectCodOnDeliveryAsync(orderId, ct);
        await NotifyOrderAsync(orderId, "OrderStatusUpdate", null, null, ct);
        return await GetAsync(orderId, null, true, ct);
    }

    // ---------------- helpers ----------------
    private Task<CustomerAddress?> ResolveAddressAsync(long userId, long? addressId, CancellationToken ct)
    {
        if (addressId is { } id)
            return _db.CustomerAddresses.FirstOrDefaultAsync(a => a.CustomerAddressId == id && a.UserId == userId && !a.IsDeleted, ct);
        return _db.CustomerAddresses
            .Where(a => a.UserId == userId && !a.IsDeleted)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.CustomerAddressId)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<OrderAddressDto?> AddressDtoAsync(long? addressId, CancellationToken ct)
    {
        if (addressId is null) return null;
        return await _db.CustomerAddresses.Where(a => a.CustomerAddressId == addressId)
            .Select(a => new OrderAddressDto(a.RecipientName, a.Phone, a.Line1, a.Line2, a.City, a.State, a.Pincode, a.Country))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<List<CartLine>> LoadCartLinesAsync(long userId, CancellationToken ct)
    {
        var cart = await _db.Carts.FirstOrDefaultAsync(c => c.UserId == userId && c.Status == "Active" && c.TenantId == Tenant, ct);
        if (cart is null) return new List<CartLine>();

        var rows = await (from ci in _db.CartItems
                          where ci.CartId == cart.CartId
                          join p in _db.Products on ci.ProductId equals p.ProductId
                          orderby ci.CartItemId
                          select new
                          {
                              ci.ProductId, ci.ProductVariantId, ci.Quantity,
                              p.Name, p.Slug, p.Sku, p.HsnCode, p.Price, p.CostPrice,
                              VariantName = ci.ProductVariantId == null ? null : _db.ProductVariants.Where(v => v.ProductVariantId == ci.ProductVariantId).Select(v => v.Name).FirstOrDefault(),
                              VariantSku = ci.ProductVariantId == null ? null : _db.ProductVariants.Where(v => v.ProductVariantId == ci.ProductVariantId).Select(v => v.Sku).FirstOrDefault(),
                              PriceAdj = ci.ProductVariantId == null ? 0m : _db.ProductVariants.Where(v => v.ProductVariantId == ci.ProductVariantId).Select(v => v.PriceAdjustment).FirstOrDefault(),
                              Available = _db.Inventory.Where(i => i.ProductId == ci.ProductId).Select(i => (int?)i.AvailableQty).Sum() ?? 0,
                          }).ToListAsync(ct);

        return rows.Select(r => new CartLine
        {
            ProductId = r.ProductId,
            VariantId = r.ProductVariantId,
            Quantity = r.Quantity,
            Name = r.Name,
            Slug = r.Slug,
            Sku = r.VariantSku ?? r.Sku,
            Hsn = r.HsnCode,
            UnitPrice = r.Price + r.PriceAdj,
            Cost = r.CostPrice,
            VariantLabel = r.VariantName,
            Available = r.Available,
        }).ToList();
    }

    private sealed class CartLine
    {
        public long ProductId { get; init; }
        public long? VariantId { get; init; }
        public int Quantity { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string? Sku { get; init; }
        public string? Hsn { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal? Cost { get; init; }
        public string? VariantLabel { get; init; }
        public int Available { get; init; }
        public decimal Rate { get; set; }
        public decimal LineSub { get; set; }
        public decimal LineTax { get; set; }
    }
}
