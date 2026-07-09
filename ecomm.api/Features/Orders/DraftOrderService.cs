using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Checkout;
using ecomm.api.Features.Coupons;
using ecomm.api.Features.Inventory;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Orders;

public sealed record DraftLineInput(long ProductId, long? VariantId, int Quantity);
public sealed record CreateDraftOrderRequest(long CustomerUserId, List<DraftLineInput> Lines, string? CouponCode, string? Notes);

public sealed record DraftOrderLineDto(
    long ProductId, long? VariantId, string Name, string? VariantLabel, int Quantity, decimal UnitPrice, decimal LineTotal, decimal TaxAmount);

public sealed record DraftOrderDto(
    long OrderId, string OrderNumber, string Status, long CustomerUserId, string? CustomerName, string? CustomerEmail,
    decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal ShippingAmount, decimal TotalAmount,
    string? CouponCode, string? Notes, IReadOnlyList<DraftOrderLineDto> Lines, DateTime CreatedAt);

public sealed record DraftOrderListItem(long OrderId, string OrderNumber, string? CustomerName, decimal TotalAmount, DateTime CreatedAt);

public interface IDraftOrderService
{
    Task<DraftOrderDto> CreateAsync(CreateDraftOrderRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<DraftOrderListItem>> ListAsync(CancellationToken ct = default);
    Task<DraftOrderDto> GetAsync(long orderId, CancellationToken ct = default);
    Task<long> ConvertAsync(long orderId, string paymentMethod, CancellationToken ct = default);
    Task DeleteAsync(long orderId, CancellationToken ct = default);
}

/// <summary>
/// Merchant-created (phone/manual) orders. A draft is an <see cref="Order"/> with Status="Draft" — no inventory
/// is held until it's converted. Pricing reuses the same tax → shipping → discount pipeline as checkout so totals
/// match. Converting reserves+commits stock, records a manual/COD payment, sets the order Confirmed and invoices it.
/// MVP: catalog products for an existing customer. Custom line items + inline customer create + emailing the draft
/// invoice are a later pass.
/// </summary>
public sealed class DraftOrderService(
    EcommerceDbContext db, ITaxService tax, IShippingService shipping, ICouponService coupons,
    IInventoryService inventory, IInvoiceService invoices) : IDraftOrderService
{
    private long Tenant => db.CurrentTenantId;

    public async Task<DraftOrderDto> CreateAsync(CreateDraftOrderRequest req, CancellationToken ct = default)
    {
        var customer = await db.Users.FirstOrDefaultAsync(u => u.UserId == req.CustomerUserId && !u.IsDeleted, ct)
            ?? throw new AppException("Select a customer for this order.");
        var lines = await ResolveLinesAsync(req.Lines ?? [], ct);

        var address = await db.CustomerAddresses
            .Where(a => a.UserId == customer.UserId && !a.IsDeleted)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.CustomerAddressId).FirstOrDefaultAsync(ct);

        var mode = await tax.GetTaxModeAsync(ct);
        var interState = await tax.IsInterStateAsync(address?.State, ct);
        decimal subtotal = 0m, taxTotal = 0m;
        foreach (var l in lines)
        {
            l.LineSub = l.UnitPrice * l.Quantity;
            var rate = await tax.ResolveRateAsync(l.Hsn, ct);
            var r = tax.ComputeLine(l.LineSub, rate, interState, mode);
            l.Rate = r.Rate; l.LineTax = r.Tax;
            subtotal += l.LineSub; taxTotal += l.LineTax;
        }

        var ship = await shipping.QuoteAsync(address?.Pincode, subtotal, ct);
        var charge = ship.Serviceable ? ship.Charge : 0m;

        var discountLines = lines.Select(l => new DiscountLine(l.ProductId, l.LineSub)).ToList();
        var coupon = await coupons.EvaluateAsync(req.CouponCode, customer.UserId, discountLines, ct);
        if (!string.IsNullOrWhiteSpace(req.CouponCode) && !coupon.Ok)
            throw new AppException(coupon.Error ?? "That discount can't be applied.");
        if (coupon.Ok && coupon.FreeShipping) charge = 0m;
        var total = subtotal + (mode == TaxMode.Exclusive ? taxTotal : 0m) + charge - coupon.Discount;
        if (total < 0m) total = 0m;

        var order = new Order
        {
            TenantId = Tenant, UserId = customer.UserId, OrderNumber = $"TMP-{Guid.NewGuid():N}"[..18],
            Status = "Draft", CouponId = coupon.Ok ? coupon.CouponId : null,
            ShippingAddressId = address?.CustomerAddressId, BillingAddressId = address?.CustomerAddressId,
            ShippingMethodId = ship.MethodId, Currency = "INR",
            Subtotal = subtotal, DiscountAmount = coupon.Discount, TaxAmount = taxTotal,
            ShippingAmount = charge, TotalAmount = total, Notes = req.Notes, CreatedAt = DateTime.UtcNow,
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        order.OrderNumber = $"DRAFT-{order.OrderId:D5}";

        foreach (var l in lines)
            db.OrderItems.Add(new OrderItem
            {
                OrderId = order.OrderId, ProductId = l.ProductId, ProductVariantId = l.VariantId, Sku = l.Sku,
                ProductName = l.Name, HsnCode = l.Hsn, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
                UnitCost = l.Cost, DiscountAmount = 0m, TaxRate = l.Rate, TaxAmount = l.LineTax,
                LineTotal = l.LineSub, CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync(ct);

        return await GetAsync(order.OrderId, ct);
    }

    public async Task<IReadOnlyList<DraftOrderListItem>> ListAsync(CancellationToken ct = default) =>
        await db.Orders.Where(o => o.Status == "Draft").OrderByDescending(o => o.OrderId)
            .Select(o => new DraftOrderListItem(o.OrderId, o.OrderNumber,
                db.Users.Where(u => u.UserId == o.UserId).Select(u => u.FullName ?? u.Email).FirstOrDefault(),
                o.TotalAmount, o.CreatedAt))
            .ToListAsync(ct);

    public async Task<DraftOrderDto> GetAsync(long orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, ct) ?? throw NotFound();
        var customer = await db.Users.Where(u => u.UserId == order.UserId)
            .Select(u => new { u.FullName, u.Email }).FirstOrDefaultAsync(ct);
        var items = await db.OrderItems.Where(i => i.OrderId == orderId).OrderBy(i => i.OrderItemId)
            .Select(i => new DraftOrderLineDto(i.ProductId, i.ProductVariantId, i.ProductName, null, i.Quantity, i.UnitPrice, i.LineTotal, i.TaxAmount))
            .ToListAsync(ct);
        var code = order.CouponId is null ? null : await db.Coupons.Where(c => c.CouponId == order.CouponId).Select(c => c.Code).FirstOrDefaultAsync(ct);
        return new DraftOrderDto(order.OrderId, order.OrderNumber, order.Status, order.UserId, customer?.FullName, customer?.Email,
            order.Subtotal, order.DiscountAmount, order.TaxAmount, order.ShippingAmount, order.TotalAmount, code, order.Notes, items, order.CreatedAt);
    }

    public async Task<long> ConvertAsync(long orderId, string paymentMethod, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.Status == "Draft", ct) ?? throw NotFound();
        var items = await db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync(ct);
        if (items.Count == 0) throw new AppException("This draft has no items.");
        var isCod = string.Equals(paymentMethod, "COD", StringComparison.OrdinalIgnoreCase);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var i in items)
            {
                var ok = await inventory.ReserveAsync(i.ProductId, i.ProductVariantId, i.Quantity, "Order", order.OrderId, ct);
                if (!ok) throw new AppException($"'{i.ProductName}' is out of stock.");
                await inventory.CommitAsync(i.ProductId, i.ProductVariantId, i.Quantity, "Order", order.OrderId, ct);
            }

            db.Payments.Add(new Payment
            {
                TenantId = Tenant, OrderId = order.OrderId, Method = isCod ? "COD" : "Manual",
                Status = isCod ? "Pending" : "Paid", Amount = order.TotalAmount, Currency = "INR", CreatedAt = DateTime.UtcNow,
            });

            order.OrderNumber = $"ORD{DateTime.UtcNow:yyyyMMdd}-{order.OrderId:D5}";
            order.Status = "Confirmed";
            order.PlacedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.OrderId, FromStatus = "Draft", ToStatus = "Confirmed",
                Notes = isCod ? "Draft converted (COD)" : "Draft converted (manual payment)", CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);

            if (order.CouponId is { } cid)
                await coupons.RecordUsageAsync(cid, order.UserId, order.OrderId, order.DiscountAmount, ct);

            await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(ct); throw; }

        await invoices.GenerateForOrderAsync(order.OrderId, ct);   // best-effort invoice after commit
        return order.OrderId;
    }

    public async Task DeleteAsync(long orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.Status == "Draft", ct) ?? throw NotFound();
        db.OrderItems.RemoveRange(await db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync(ct));
        db.Orders.Remove(order);   // no inventory was held for a draft
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers ----
    private async Task<List<PricedLine>> ResolveLinesAsync(List<DraftLineInput> inputs, CancellationToken ct)
    {
        var lines = new List<PricedLine>();
        foreach (var inp in inputs.Where(i => i.Quantity > 0))
        {
            var p = await db.Products.FirstOrDefaultAsync(x => x.ProductId == inp.ProductId, ct)
                ?? throw new AppException($"Product {inp.ProductId} not found.");
            decimal priceAdj = 0m; string? vSku = null;
            if (inp.VariantId is { } vid)
            {
                var v = await db.ProductVariants.FirstOrDefaultAsync(x => x.ProductVariantId == vid, ct);
                if (v is not null) { priceAdj = v.PriceAdjustment; vSku = v.Sku; }
            }
            lines.Add(new PricedLine
            {
                ProductId = p.ProductId, VariantId = inp.VariantId, Name = p.Name, Sku = vSku ?? p.Sku,
                Hsn = p.HsnCode, UnitPrice = p.Price + priceAdj, Cost = p.CostPrice, Quantity = inp.Quantity,
            });
        }
        if (lines.Count == 0) throw new AppException("Add at least one product to the order.");
        return lines;
    }

    private static AppException NotFound() => new("Draft order not found.", StatusCodes.Status404NotFound);

    private sealed class PricedLine
    {
        public long ProductId; public long? VariantId; public string Name = ""; public string? Sku; public string? Hsn;
        public decimal UnitPrice; public decimal? Cost; public int Quantity;
        public decimal LineSub; public decimal Rate; public decimal LineTax;
    }
}
