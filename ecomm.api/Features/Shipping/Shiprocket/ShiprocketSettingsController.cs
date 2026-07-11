using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Shipping.Shiprocket;

/// <summary>
/// Merchant-admin fulfillment settings: choose Self (manual) vs Shiprocket, and connect the
/// store's own Shiprocket account. The Shiprocket password is write-only.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/shipping/shiprocket")]
public sealed class ShiprocketSettingsController(IShiprocketSettingsService svc) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(ApiResponse<ShiprocketSettingsDto>.Ok(await svc.GetAsync(ct)));

    [HttpPut("settings")]
    public async Task<IActionResult> Update(UpdateShiprocketSettingsRequest req, CancellationToken ct)
        => Ok(ApiResponse<ShiprocketSettingsDto>.Ok(await svc.UpdateAsync(req, ct), "Fulfillment settings saved."));
}
