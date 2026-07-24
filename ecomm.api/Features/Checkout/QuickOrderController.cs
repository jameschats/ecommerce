using System.Security.Claims;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Checkout;

/// <summary>
/// Quick-order checkout (design.md §6–7). Anonymous: the buyer builds and prices an order
/// freely; identity is only required at placement, which is the next slice.
/// </summary>
[ApiController]
[Route("api/quick-order")]
public sealed class QuickOrderController : ControllerBase
{
    private readonly IQuickOrderService _quickOrder;

    public QuickOrderController(IQuickOrderService quickOrder) => _quickOrder = quickOrder;

    /// <summary>States, minimum order amounts and packing charge — everything the order form needs up front.</summary>
    [HttpGet("config")]
    public async Task<IActionResult> Config(CancellationToken ct)
        => Ok(ApiResponse<QuickOrderConfigDto>.Ok(await _quickOrder.GetConfigAsync(ct)));

    /// <summary>
    /// Prices a basket. POST rather than GET because the line list can be long and is not
    /// cacheable — and because this is the server's number, not the browser's.
    /// </summary>
    [HttpPost("quote")]
    public async Task<IActionResult> Quote([FromBody] QuickOrderQuoteRequest req, CancellationToken ct)
        => Ok(ApiResponse<QuickOrderQuoteDto>.Ok(await _quickOrder.QuoteAsync(req, ct)));

    /// <summary>
    /// Places the order. The only authenticated endpoint here — the buyer builds and prices
    /// freely, and identity is required at the point it becomes a commitment (design.md §7.2).
    /// </summary>
    [Authorize]
    [HttpPost("place")]
    public async Task<IActionResult> Place([FromBody] PlaceQuickOrderRequest req, CancellationToken ct)
    {
        var userId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new AppException("Please sign in to place your order.", StatusCodes.Status401Unauthorized);

        return Ok(ApiResponse<PlaceQuickOrderResult>.Ok(
            await _quickOrder.PlaceAsync(userId, req, ct), "Order placed."));
    }
}
