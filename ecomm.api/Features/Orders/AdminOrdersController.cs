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
    public async Task<IActionResult> CreateShipment(
        long id,
        CreateShipmentRequest request,
        [FromServices] Notifications.IOrderMailer mailer,
        CancellationToken ct)
    {
        var dto = await _orders.CreateShipmentAsync(id, request, CurrentUserId, ct);
        if (dto is null) return NotFound(ApiResponse<object>.Fail("Order not found."));

        await mailer.SendDispatchedAsync(id, request.Courier, request.TrackingNumber, ct);
        return Ok(ApiResponse<OrderDto>.Ok(dto, "Shipment created — customer notified."));
    }

    [HttpPost("{id:long}/deliver")]
    public async Task<IActionResult> MarkDelivered(
        long id,
        [FromServices] Notifications.IOrderMailer mailer,
        CancellationToken ct)
    {
        var dto = await _orders.MarkDeliveredAsync(id, CurrentUserId, ct);
        if (dto is null) return NotFound(ApiResponse<object>.Fail("Order not found."));

        await mailer.SendDeliveredAsync(id, ct);
        return Ok(ApiResponse<OrderDto>.Ok(dto, "Marked delivered — customer notified."));
    }

    /// <summary>
    /// A pre-written WhatsApp message and a wa.me link for it (design.md §9.4).
    ///
    /// Click-to-send rather than the Cloud API: Meta's template approval takes days to
    /// weeks and is outside our control, so launching on it would make their queue our
    /// blocker. A human tapping send on a correct, pre-written message is fine at this
    /// order volume, and switching to automated sending later changes nothing here.
    /// </summary>
    [HttpGet("{id:long}/whatsapp")]
    public async Task<IActionResult> WhatsAppLink(
        long id,
        [FromQuery] string kind,
        [FromServices] Notifications.IOrderMailer mailer,
        CancellationToken ct)
    {
        var built = await mailer.BuildWhatsAppMessageAsync(id, kind ?? "placed", ct);
        if (built is null)
            return NotFound(ApiResponse<object>.Fail("No mobile number on this order."));

        var (mobile, message) = built.Value;
        var url = $"https://wa.me/{mobile}?text={Uri.EscapeDataString(message)}";
        return Ok(ApiResponse<object>.Ok(new { mobile, message, url }));
    }

    [HttpGet("{id:long}/invoice")]
    public async Task<IActionResult> Invoice(long id, CancellationToken ct)
    {
        var pdf = await _invoices.RenderPdfAsync(id, null, true, ct);
        return pdf is null ? NotFound(ApiResponse<object>.Fail("Invoice not available.")) : File(pdf.Bytes, "application/pdf", pdf.FileName);
    }
}

public sealed record UpdateOrderStatusRequest(string Status);
