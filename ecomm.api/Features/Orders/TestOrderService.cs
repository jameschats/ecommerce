using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Orders;

public sealed record TestOrderResult(long OrderId, string OrderNumber, string ProductName, decimal TotalAmount);

public interface ITestOrderService
{
    Task<TestOrderResult> PlaceAsync(long merchantUserId, CancellationToken ct = default);
}

/// <summary>
/// The "try a test order" walkthrough (M10b). Runs a real order through the real pipeline — pricing, GST,
/// shipping, inventory, invoice, notifications — so the merchant sees the machine work before a customer does,
/// and so misconfiguration (no serviceable zone, missing store details on the invoice) surfaces early.
///
/// Reuses <see cref="IDraftOrderService"/> rather than the cart checkout path: a draft is created for an existing
/// user and converted, which never touches the merchant's own shopping cart. The order is flagged
/// <c>IsTest</c> — kept, not deleted, so invoice numbering stays contiguous — and excluded from all analytics.
/// It holds one unit of real stock; cancelling it restocks through the normal cancel flow.
/// </summary>
public sealed class TestOrderService(EcommerceDbContext db, IDraftOrderService drafts) : ITestOrderService
{
    public async Task<TestOrderResult> PlaceAsync(long merchantUserId, CancellationToken ct = default)
    {
        var product = await db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.ProductId)
            .Select(p => new { p.ProductId, p.Name })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException("Add a product first — a test order needs something to buy.");

        var draft = await drafts.CreateAsync(new CreateDraftOrderRequest(
            merchantUserId,
            [new DraftLineInput(product.ProductId, null, 1)],
            CouponCode: null,
            Notes: "Test order placed from Getting started. Cancel it to return the stock."), ct);

        // COD keeps the walkthrough self-contained — no gateway keys needed to see the flow end to end.
        var orderId = await drafts.ConvertAsync(draft.OrderId, "COD", ct);

        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, ct)
            ?? throw new AppException("The test order could not be created.", 500);
        order.IsTest = true;
        await db.SaveChangesAsync(ct);

        return new TestOrderResult(order.OrderId, order.OrderNumber, product.Name, order.TotalAmount);
    }
}
