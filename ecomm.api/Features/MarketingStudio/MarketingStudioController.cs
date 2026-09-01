using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>
/// Marketing Studio (MS0) — the merchant's AI creative + social-publishing workspace. Gated behind
/// the <c>marketing_studio</c> plan feature. Own <c>/api/marketing/*</c> prefix and bounded module so
/// it can be lifted to a separate service later (marketing-studio-plan.md §3.10). MS0 ships the
/// visual brand kit; connections / posters / scheduler / video land in later milestones.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[RequiresFeature("marketing_studio")]
[Route("api/marketing")]
public sealed class MarketingStudioController(
    IMarketingBrandService brand, IMarketingPlanSettingsService planSettings) : ControllerBase
{
    [HttpGet("brand")]
    public async Task<IActionResult> GetBrand(CancellationToken ct)
        => Ok(ApiResponse<MarketingBrandDto>.Ok(await brand.GetAsync(ct)));

    [HttpPut("brand")]
    public async Task<IActionResult> SaveBrand(MarketingBrandDto request, CancellationToken ct)
        => Ok(ApiResponse<MarketingBrandDto>.Ok(await brand.SaveAsync(request, ct), "Brand kit saved."));

    [HttpGet("plan/settings")]
    public async Task<IActionResult> GetPlanSettings(CancellationToken ct)
        => Ok(ApiResponse<MarketingPlanSettingsDto>.Ok(await planSettings.GetAsync(ct)));

    [HttpPut("plan/settings")]
    public async Task<IActionResult> SavePlanSettings(MarketingPlanSettingsDto request, CancellationToken ct)
        => Ok(ApiResponse<MarketingPlanSettingsDto>.Ok(await planSettings.SaveAsync(request, ct), "Preferences saved."));
}
