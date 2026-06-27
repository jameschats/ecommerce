using System.Security.Claims;
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
    public async Task<IActionResult> Quote([FromQuery] long? addressId, CancellationToken ct)
        => Ok(ApiResponse<CheckoutQuoteDto>.Ok(await _orders.QuoteAsync(CurrentUserId, addressId, ct)));

    [HttpPost]
    public async Task<IActionResult> Place(PlaceOrderRequest request, CancellationToken ct)
        => Ok(ApiResponse<PlaceOrderResult>.Ok(await _orders.PlaceOrderAsync(CurrentUserId, request, ct), "Order created."));

    [HttpPost("{id:long}/confirm")]
    public async Task<IActionResult> Confirm(long id, ConfirmPaymentRequest request, CancellationToken ct)
        => Ok(ApiResponse<OrderDto>.Ok(await _orders.ConfirmPaymentAsync(CurrentUserId, id, request, ct), "Payment confirmed."));

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancelOrderRequest request, CancellationToken ct)
        => Ok(ApiResponse<OrderDto>.Ok(await _orders.CancelOrderAsync(CurrentUserId, id, request, false, ct), "Order cancelled."));

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
