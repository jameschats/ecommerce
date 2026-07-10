using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Settings;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/checkout-settings")]
public sealed class CheckoutSettingsAdminController(ICheckoutSettingsService settings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(ApiResponse<CheckoutSettingsDto>.Ok(await settings.GetAsync(ct)));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateCheckoutSettingsRequest req, CancellationToken ct)
        => Ok(ApiResponse<CheckoutSettingsDto>.Ok(await settings.UpdateAsync(req, ct), "Checkout settings saved."));
}

/// <summary>Public: checkout/account settings the storefront needs to render.</summary>
[ApiController]
[Route("api/catalog/checkout-settings")]
public sealed class CheckoutSettingsPublicController(ICheckoutSettingsService settings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(ApiResponse<PublicCheckoutSettingsDto>.Ok(await settings.GetPublicAsync(ct)));
}
