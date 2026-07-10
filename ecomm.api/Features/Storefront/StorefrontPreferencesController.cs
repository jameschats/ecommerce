using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Storefront;

public sealed record CheckGateRequest(string? Password);

/// <summary>Merchant-admin storefront preferences (SEO + pre-launch password).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/storefront-preferences")]
public sealed class StorefrontPreferencesAdminController(IStorefrontPreferencesService prefs) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(ApiResponse<StorefrontPreferencesDto>.Ok(await prefs.GetAsync(ct)));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateStorefrontPreferencesRequest req, CancellationToken ct)
        => Ok(ApiResponse<StorefrontPreferencesDto>.Ok(await prefs.UpdateAsync(req, ct), "Preferences saved."));
}

/// <summary>Public: store SEO defaults + pre-launch gate status/check.</summary>
[ApiController]
[Route("api/catalog/storefront")]
public sealed class StorefrontPreferencesPublicController(IStorefrontPreferencesService prefs) : ControllerBase
{
    [HttpGet("seo")]
    public async Task<IActionResult> Seo(CancellationToken ct)
        => Ok(ApiResponse<StoreSeoDto>.Ok(await prefs.GetSeoAsync(ct)));

    [HttpGet("gate")]
    public async Task<IActionResult> Gate(CancellationToken ct)
        => Ok(ApiResponse<StoreGateDto>.Ok(await prefs.GetGateAsync(ct)));

    [HttpPost("gate")]
    public async Task<IActionResult> CheckGate(CheckGateRequest req, CancellationToken ct)
        => Ok(ApiResponse<object>.Ok(new { ok = await prefs.CheckPasswordAsync(req.Password, ct) }));
}
