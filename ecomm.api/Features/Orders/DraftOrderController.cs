using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Orders;

/// <summary>Merchant-admin Draft / manual orders (phone/in-person sales).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/draft-orders")]
public sealed class DraftOrderController(IDraftOrderService drafts) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<DraftOrderListItem>>.Ok(await drafts.ListAsync(ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Ok(ApiResponse<DraftOrderDto>.Ok(await drafts.GetAsync(id, ct)));

    [HttpPost]
    public async Task<IActionResult> Create(CreateDraftOrderRequest req, CancellationToken ct)
        => Ok(ApiResponse<DraftOrderDto>.Ok(await drafts.CreateAsync(req, ct), "Draft order created."));

    [HttpPost("{id:long}/convert")]
    public async Task<IActionResult> Convert(long id, [FromQuery] string paymentMethod, CancellationToken ct)
        => Ok(ApiResponse<object>.Ok(new { orderId = await drafts.ConvertAsync(id, paymentMethod, ct) }, "Order created."));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    { await drafts.DeleteAsync(id, ct); return Ok(ApiResponse<object>.Ok(new { }, "Draft deleted.")); }
}
