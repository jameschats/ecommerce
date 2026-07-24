using ecomm.api.Common.Models;
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
}
