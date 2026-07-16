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

    [HttpPost("{id:long}/shipment")]
    public async Task<IActionResult> CreateShipment(long id, CreateShipmentRequest request, CancellationToken ct)
    {
        var dto = await _orders.CreateShipmentAsync(id, request, CurrentUserId, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Order not found.")) : Ok(ApiResponse<OrderDto>.Ok(dto, "Shipment created — customer notified."));
    }

    [HttpPost("{id:long}/ship-shiprocket")]
    public async Task<IActionResult> ShipWithShiprocket(long id, CancellationToken ct)
    {
        var dto = await _orders.ShipWithShiprocketAsync(id, CurrentUserId, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Order not found.")) : Ok(ApiResponse<OrderDto>.Ok(dto, "Order pushed to Shiprocket."));
    }

    [HttpPost("{id:long}/shiprocket-pickup")]
    public async Task<IActionResult> ShiprocketPickup(long id, CancellationToken ct)
    {
        var dto = await _orders.SchedulePickupAsync(id, CurrentUserId, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Order not found.")) : Ok(ApiResponse<OrderDto>.Ok(dto, "Pickup scheduled with Shiprocket."));
    }

    [HttpPost("{id:long}/shiprocket-label")]
    public async Task<IActionResult> ShiprocketLabel(long id, CancellationToken ct)
    {
        var url = await _orders.GenerateShiprocketLabelAsync(id, ct);
        return string.IsNullOrEmpty(url)
            ? NotFound(ApiResponse<object>.Fail("Label is not ready yet — try again shortly."))
            : Ok(ApiResponse<object>.Ok(new { labelUrl = url }, "Label ready."));
    }

    [HttpPost("{id:long}/reship")]
    public async Task<IActionResult> Reship(long id, CancellationToken ct)
    {
        var dto = await _orders.ReshipAsync(id, CurrentUserId, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Order not found.")) : Ok(ApiResponse<OrderDto>.Ok(dto, "Order is back to Packed — ship it again below."));
    }

    [HttpPost("{id:long}/deliver")]
    public async Task<IActionResult> MarkDelivered(long id, CancellationToken ct)
    {
        var dto = await _orders.MarkDeliveredAsync(id, CurrentUserId, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Order not found.")) : Ok(ApiResponse<OrderDto>.Ok(dto, "Marked delivered."));
    }

    [HttpGet("{id:long}/invoice")]
    public async Task<IActionResult> Invoice(long id, CancellationToken ct)
    {
        var pdf = await _invoices.RenderPdfAsync(id, null, true, ct);
        return pdf is null ? NotFound(ApiResponse<object>.Fail("Invoice not available.")) : File(pdf.Bytes, "application/pdf", pdf.FileName);
    }
}

public sealed record UpdateOrderStatusRequest(string Status);
