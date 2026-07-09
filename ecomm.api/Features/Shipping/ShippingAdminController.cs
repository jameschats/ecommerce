using ecomm.api.Common.Models;
using ecomm.api.Features.Shipping.Shiprocket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Shipping;

/// <summary>Merchant-admin Shipping: methods (rates/free-threshold/ETA) + pincode zones.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/shipping")]
public sealed class ShippingAdminController(IShippingAdminService shipping, IShiprocketClient shiprocket) : ControllerBase
{
    /// <summary>Whether live courier rates (Shiprocket) are wired up on the platform.</summary>
    [HttpGet("integration")]
    public IActionResult Integration()
        => Ok(ApiResponse<object>.Ok(new { shiprocketEnabled = shiprocket.Enabled }));

    // ---- methods ----
    [HttpGet("methods")]
    public async Task<IActionResult> Methods(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ShippingMethodDto>>.Ok(await shipping.ListMethodsAsync(ct)));

    [HttpPost("methods")]
    public async Task<IActionResult> CreateMethod(SaveShippingMethodRequest req, CancellationToken ct)
        => Ok(ApiResponse<ShippingMethodDto>.Ok(await shipping.CreateMethodAsync(req, ct), "Shipping method added."));

    [HttpPut("methods/{id:long}")]
    public async Task<IActionResult> UpdateMethod(long id, SaveShippingMethodRequest req, CancellationToken ct)
        => Ok(ApiResponse<ShippingMethodDto>.Ok(await shipping.UpdateMethodAsync(id, req, ct), "Shipping method saved."));

    [HttpDelete("methods/{id:long}")]
    public async Task<IActionResult> DeleteMethod(long id, CancellationToken ct)
    { await shipping.DeleteMethodAsync(id, ct); return Ok(ApiResponse<object>.Ok(new { }, "Shipping method deleted.")); }

    // ---- zones ----
    [HttpGet("zones")]
    public async Task<IActionResult> Zones(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ShippingZoneDto>>.Ok(await shipping.ListZonesAsync(ct)));

    [HttpPost("zones")]
    public async Task<IActionResult> CreateZone(SaveShippingZoneRequest req, CancellationToken ct)
        => Ok(ApiResponse<ShippingZoneDto>.Ok(await shipping.CreateZoneAsync(req, ct), "Zone added."));

    [HttpPut("zones/{id:long}")]
    public async Task<IActionResult> UpdateZone(long id, SaveShippingZoneRequest req, CancellationToken ct)
        => Ok(ApiResponse<ShippingZoneDto>.Ok(await shipping.UpdateZoneAsync(id, req, ct), "Zone saved."));

    [HttpDelete("zones/{id:long}")]
    public async Task<IActionResult> DeleteZone(long id, CancellationToken ct)
    { await shipping.DeleteZoneAsync(id, ct); return Ok(ApiResponse<object>.Ok(new { }, "Zone deleted.")); }
}
