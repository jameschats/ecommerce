using System.Security.Claims;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Orders;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    private readonly IInvoiceService _invoices;
    public OrdersController(IOrderService orders, IInvoiceService invoices)
    {
        _orders = orders;
        _invoices = invoices;
    }

    private long CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    [HttpGet("quote")]
    public async Task<IActionResult> Quote([FromQuery] long? addressId, [FromQuery] string? coupon, CancellationToken ct)
        => Ok(ApiResponse<CheckoutQuoteDto>.Ok(await _orders.QuoteAsync(CurrentUserId, addressId, coupon, ct)));

    [HttpPost]
    public async Task<IActionResult> Place(PlaceOrderRequest request, CancellationToken ct)
        => Ok(ApiResponse<PlaceOrderResult>.Ok(await _orders.PlaceOrderAsync(CurrentUserId, request, ct), "Order created."));

    [HttpPost("{id:long}/confirm")]
    public async Task<IActionResult> Confirm(long id, ConfirmPaymentRequest request, CancellationToken ct)
        => Ok(ApiResponse<OrderDto>.Ok(await _orders.ConfirmPaymentAsync(CurrentUserId, id, request, ct), "Payment confirmed."));

    /// <summary>
    /// Locked, not removed — CancelOrderAsync(isAdmin: false) still exists and works exactly
    /// as before, just unreachable from this customer-facing route. Cancellation is now staff-
    /// only: the customer calls or emails, and an admin cancels via the admin order screen.
    /// Left as a guard clause here (rather than deleting the route/action) so restoring
    /// self-service cancellation later is a one-line revert, not a rebuild.
    /// </summary>
    [HttpPost("{id:long}/cancel")]
    public Task<IActionResult> Cancel(long id, CancelOrderRequest request, CancellationToken ct) =>
        throw new AppException(
            "Orders can no longer be cancelled online. Please call or email us and we'll cancel it for you.");

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<OrderListItem>>.Ok(await _orders.ListMineAsync(CurrentUserId, ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var dto = await _orders.GetAsync(id, CurrentUserId, false, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Order not found.")) : Ok(ApiResponse<OrderDto>.Ok(dto));
    }

    [HttpGet("{id:long}/invoice")]
    public async Task<IActionResult> Invoice(long id, CancellationToken ct)
    {
        var pdf = await _invoices.RenderPdfAsync(id, CurrentUserId, false, ct);
        return pdf is null ? NotFound(ApiResponse<object>.Fail("Invoice not available.")) : File(pdf.Bytes, "application/pdf", pdf.FileName);
    }
}
