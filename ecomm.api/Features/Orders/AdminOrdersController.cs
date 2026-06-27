using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Orders;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/orders")]
public class AdminOrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    private readonly IInvoiceService _invoices;
    public AdminOrdersController(IOrderService orders, IInvoiceService invoices)
    {
        _orders = orders;
        _invoices = invoices;
    }

    private long? CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<OrderListItem>>.Ok(await _orders.ListAllAsync(status, page, pageSize, ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var dto = await _orders.GetAsync(id, null, true, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Order not found.")) : Ok(ApiResponse<OrderDto>.Ok(dto));
    }

    [HttpPost("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(long id, UpdateOrderStatusRequest request, CancellationToken ct)
    {
        var dto = await _orders.UpdateStatusAsync(id, request.Status, CurrentUserId, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Order not found.")) : Ok(ApiResponse<OrderDto>.Ok(dto, "Status updated."));
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancelOrderRequest request, CancellationToken ct)
        => Ok(ApiResponse<OrderDto>.Ok(await _orders.CancelOrderAsync(CurrentUserId ?? 0, id, request, true, ct), "Order cancelled."));

    [HttpGet("{id:long}/invoice")]
    public async Task<IActionResult> Invoice(long id, CancellationToken ct)
    {
        var pdf = await _invoices.RenderPdfAsync(id, null, true, ct);
        return pdf is null ? NotFound(ApiResponse<object>.Fail("Invoice not available.")) : File(pdf.Bytes, "application/pdf", pdf.FileName);
    }
}

public sealed record UpdateOrderStatusRequest(string Status);
