using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Theme;

/// <summary>Public: active theme for the storefront.</summary>
[ApiController]
[Route("api/theme")]
public sealed class ThemeController : ControllerBase
{
    private readonly IThemeService _theme;

    public ThemeController(IThemeService theme) => _theme = theme;

    [HttpGet]
    public async Task<IActionResult> Active(CancellationToken ct)
        => Ok(ApiResponse<ThemeDto>.Ok(await _theme.GetActiveAsync(ct)));
}

/// <summary>Admin: view + update the active theme.</summary>
[ApiController]
[Route("api/admin/theme")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.ThemeManage)]
public sealed class ThemeAdminController : ControllerBase
{
    private readonly IThemeService _theme;

    public ThemeAdminController(IThemeService theme) => _theme = theme;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(ApiResponse<ThemeDto>.Ok(await _theme.GetActiveAsync(ct)));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateThemeRequest request, CancellationToken ct)
        => Ok(ApiResponse<ThemeDto>.Ok(await _theme.UpdateAsync(request.Settings ?? new(), ct), "Theme updated."));
}
