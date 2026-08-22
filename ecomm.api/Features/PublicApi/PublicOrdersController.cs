using ecomm.api.Common.Models;
using ecomm.api.Features.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ecomm.api.Features.PublicApi;

[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthDefaults.Scheme)]
[EnableRateLimiting("public-api")]
[Route("api/public/v1/orders")]
public sealed class PublicOrdersController(IOrderService orders) : ControllerBase
{
    [HttpGet]
    [RequiresScope("orders:read")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await orders.ListAllAsync(status, page, Math.Clamp(pageSize, 1, 100), ct);
        var items = result.Items.Select(o => new PublicOrderSummaryDto(o.orderId, o.orderNumber, o.status, o.totalAmount, o.itemCount, o.placedAt)).ToList();
        return Ok(ApiResponse<PagedResult<PublicOrderSummaryDto>>.Ok(new PagedResult<PublicOrderSummaryDto>
        {
            Items = items, Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount,
        }));
    }

    [HttpGet("{id:long}")]
    [RequiresScope("orders:read")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var o = await orders.GetAsync(id, userId: null, isAdmin: true, ct);
        if (o is null) return NotFound(ApiResponse<object>.Fail("Order not found."));
        return Ok(ApiResponse<PublicOrderDto>.Ok(Map(o)));
    }

    private static PublicOrderDto Map(OrderDto o) => new(
        o.orderId, o.orderNumber, o.status, o.currency, o.totalAmount, o.placedAt,
        o.items.Select(i => new PublicOrderItemDto(i.productId, i.productName, i.sku, i.quantity, i.unitPrice, i.lineTotal)).ToList(),
        o.shipment?.status, o.shipment?.trackingNumber);
}
