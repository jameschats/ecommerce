using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Checkout;
using ecomm.api.Features.Inventory;
using ecomm.api.Features.Payments;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Orders;

public interface IOrderService
{
    Task<CheckoutQuoteDto> QuoteAsync(long userId, long? shippingAddressId, CancellationToken ct = default);
    Task<PlaceOrderResult> PlaceOrderAsync(long userId, PlaceOrderRequest req, CancellationToken ct = default);
    Task<OrderDto> ConfirmPaymentAsync(long userId, long orderId, ConfirmPaymentRequest req, CancellationToken ct = default);
    Task<OrderDto> CancelOrderAsync(long userId, long orderId, CancelOrderRequest req, bool isAdmin, CancellationToken ct = default);
    Task<List<OrderListItem>> ListMineAsync(long userId, CancellationToken ct = default);
    Task<OrderDto?> GetAsync(long orderId, long? userId, bool isAdmin, CancellationToken ct = default);
    Task<PagedResult<OrderListItem>> ListAllAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<OrderDto?> UpdateStatusAsync(long orderId, string toStatus, long? userId, CancellationToken ct = default);
}

public sealed class OrderService : IOrderService
{
    private const long Tenant = 1;
    private static readonly string[] AdminFlow = { "Paid", "Packed", "Shipped", "Delivered" };

    private readonly EcommerceDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly ITaxService _tax;
    private readonly IShippingService _shipping;
    private readonly IPaymentGateway _gateway;
    private readonly IInvoiceService _invoices;
    private readonly ILogger<OrderService> _log;

    public OrderService(EcommerceDbContext db, IInventoryService inventory, ITaxService tax,
        IShippingService shipping, IPaymentGateway gateway, IInvoiceService invoices, ILogger<OrderService> log)
    {
        _db = db; _inventory = inventory; _tax = tax; _shipping = shipping;
        _gateway = gateway; _invoices = invoices; _log = log;
    }

    // ---------------- Quote ----------------
    public async Task<CheckoutQuoteDto> QuoteAsync(long userId, long? shippingAddressId, CancellationToken ct = default)
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
        // Exclusive adds tax on top; Inclusive/None already have it in the listed price.
        var total = subtotal + (mode == TaxMode.Exclusive ? taxTotal : 0m) + charge;

        return new CheckoutQuoteDto(serviceable, message, quoteLines,
            subtotal, taxTotal, cgst, sgst, igst, interState,
            charge, ship.MethodName, ship.EstimatedDays,
            total, address?.CustomerAddressId, mode);
    }

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
        var total = subtotal + (mode == TaxMode.Exclusive ? taxTotal : 0m) + ship.Charge;

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
                Subtotal = subtotal,
                DiscountAmount = 0m,
                TaxAmount = taxTotal,
                ShippingAmount = ship.Charge,
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
                    DiscountAmount = 0m,
                    TaxRate = l.Rate,
                    TaxAmount = l.LineTax,
                    LineTotal = l.LineSub,
                    CreatedAt = DateTime.UtcNow,
                });
            }

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

            cart.Status = "Converted";
            cart.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new PlaceOrderResult(order.OrderId, order.OrderNumber, total, "INR",
                new PaymentInit(_gateway.Name, _gateway.PublicKey, gatewayOrder.GatewayOrderId, payment.PaymentId, total, "INR"));
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
        var wasPaid = order.Status is "Paid" or "Packed";

        foreach (var it in items)
        {
            if (wasPaid) await _inventory.RestockAsync(it.ProductId, it.ProductVariantId, it.Quantity, "Order", orderId, ct);
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

        order.Status = "Cancelled";
        order.UpdatedAt = DateTime.UtcNow;
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId, FromStatus = wasPaid ? "Paid" : "Pending", ToStatus = "Cancelled",
            Notes = req.Reason ?? "Cancelled", ChangedBy = userId, CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(orderId, userId, isAdmin, ct))!;
    }

    // ---------------- Reads ----------------
    public Task<List<OrderListItem>> ListMineAsync(long userId, CancellationToken ct = default) =>
        _db.Orders.Where(o => o.UserId == userId && o.TenantId == Tenant)
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

        var canCancel = order.Status is "Pending" or "Paid" or "Packed";

        return new OrderDto(order.OrderId, order.OrderNumber, order.Status, order.Currency,
            order.Subtotal, order.DiscountAmount, order.TaxAmount, order.ShippingAmount, order.TotalAmount,
            order.PlacedAt, order.CreatedAt, items, ship, bill,
            payment?.Method, payment?.Status, invoice?.InvoiceId, invoice?.InvoiceNumber, canCancel);
    }

    public async Task<PagedResult<OrderListItem>> ListAllAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Orders.Where(o => o.TenantId == Tenant);
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

    public async Task<OrderDto?> UpdateStatusAsync(long orderId, string toStatus, long? userId, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return null;
        if (!AdminFlow.Contains(toStatus)) throw new AppException($"'{toStatus}' is not a valid status.");
        var fromIdx = Array.IndexOf(AdminFlow, order.Status);
        var toIdx = Array.IndexOf(AdminFlow, toStatus);
        if (fromIdx < 0) throw new AppException($"An order that is {order.Status} cannot change to {toStatus}.");
        if (toIdx != fromIdx + 1) throw new AppException($"Cannot move from {order.Status} to {toStatus}.");

        var from = order.Status;
        order.Status = toStatus;
        order.UpdatedAt = DateTime.UtcNow;
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId, FromStatus = from, ToStatus = toStatus, Notes = "Status updated by admin", ChangedBy = userId, CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
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
                              p.Name, p.Slug, p.Sku, p.HsnCode, p.Price,
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
        public string? VariantLabel { get; init; }
        public int Available { get; init; }
        public decimal Rate { get; set; }
        public decimal LineSub { get; set; }
        public decimal LineTax { get; set; }
    }
}
