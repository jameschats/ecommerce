using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

/// <summary>
/// Guest order tracking. Anonymous and rate-limited: order numbers are enumerable, so this is
/// the one endpoint where brute force is a realistic concern.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/orders/lookup")]
public sealed class OrderLookupController(IOrderLookupService lookup) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting("lookup")]
    public async Task<IActionResult> Find([FromQuery] string orderNumber, [FromQuery] string email, CancellationToken ct)
    {
        var result = await lookup.FindAsync(orderNumber, email, ct);

        // Same answer whether the order doesn't exist or the email doesn't match — telling
        // those apart would confirm which order numbers are real.
        return result is null
            ? NotFound(ApiResponse<object>.Fail("We couldn't find an order with those details."))
            : Ok(ApiResponse<OrderLookupDto>.Ok(result));
    }
}

/// <summary>The merchant's "try a test order" walkthrough (M10b).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/test-order")]
public sealed class TestOrderController(ITestOrderService testOrders) : ControllerBase
{
    private long? CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpPost]
    public async Task<IActionResult> Place(CancellationToken ct)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        var result = await testOrders.PlaceAsync(userId, ct);
        return Ok(ApiResponse<TestOrderResult>.Ok(result, "Test order placed."));
    }
}
