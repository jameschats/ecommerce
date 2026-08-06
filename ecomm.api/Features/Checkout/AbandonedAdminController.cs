using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Checkout;

/// <summary>Baskets a signed-in shopper built and did not order.</summary>
[ApiController]
[Route("api/admin/abandoned")]
[Authorize(Roles = "Admin")]
public sealed class AbandonedAdminController : ControllerBase
{
    private readonly IAbandonedEstimateService _estimates;
    public AbandonedAdminController(IAbandonedEstimateService estimates) => _estimates = estimates;

    /// <summary>
    /// Idle for at least <paramref name="idleHours"/>. Defaults to a day: shorter and the
    /// list fills with people who are still mid-basket and would be chased about a decision
    /// they have not finished making.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int idleHours = 24, CancellationToken ct = default)
        => Ok(ApiResponse<List<AbandonedEstimateDto>>.Ok(await _estimates.ListAsync(idleHours, ct)));

    [HttpPost("{cartId:long}/remind")]
    public async Task<IActionResult> Remind(long cartId, CancellationToken ct)
        => await _estimates.RemindAsync(cartId, ct)
            ? Ok(ApiResponse<object>.Ok(new { sent = true }, "Reminder sent."))
            : NotFound(ApiResponse<object>.Fail("That basket no longer exists."));
}
