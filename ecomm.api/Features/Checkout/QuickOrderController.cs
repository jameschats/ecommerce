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
    {
        // Not [Authorize] — pricing is anonymous — but if the buyer already happens to be
        // signed in, using their real id here lets a per-user coupon limit be reported
        // accurately before they submit, rather than only at Place.
        var userId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        return Ok(ApiResponse<QuickOrderQuoteDto>.Ok(await _quickOrder.QuoteAsync(req, userId, ct)));
    }

    /// <summary>
    /// The same basket as a printable quotation.
    ///
    /// Anonymous, like pricing itself: a dealer working up a quote for their own customer
    /// has not committed to anything, and making them sign in first would be a gate in front
    /// of a sales tool. Priced through the same QuoteAsync call, so paper and screen agree.
    /// </summary>
    [HttpPost("quote/pdf")]
    public async Task<IActionResult> QuotePdf(
        [FromBody] QuoteDocumentRequest req,
        [FromServices] IQuoteDocumentService quotes,
        CancellationToken ct)
    {
        var pdf = await quotes.RenderAsync(
            new QuickOrderQuoteRequest(req.Lines, req.State), req.CustomerName, ct);
        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    /// <summary>
    /// Mirrors the signed-in shopper's estimate to the server so an unfinished basket is
    /// visible to the shop. Fire-and-forget from the client's point of view: it is a
    /// convenience for the shop, and must never interrupt someone building an order.
    /// </summary>
    [Authorize]
    [HttpPost("estimate")]
    public async Task<IActionResult> SaveEstimate(
        [FromBody] SaveEstimateRequest req,
        [FromServices] IAbandonedEstimateService estimates,
        CancellationToken ct)
    {
        var userId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        if (userId == 0) return Ok(ApiResponse<object>.Ok(new { saved = false }));

        await estimates.SaveAsync(userId, req, ct);
        return Ok(ApiResponse<object>.Ok(new { saved = true }));
    }

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
