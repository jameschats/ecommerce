using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Navigation;

/// <summary>Merchant-admin navigation: menus + URL redirects.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/navigation")]
public sealed class NavigationAdminController(INavigationService nav) : ControllerBase
{
    [HttpGet("menus")]
    public async Task<IActionResult> Menus(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<MenuDto>>.Ok(await nav.ListMenusAsync(ct)));

    [HttpGet("menus/{handle}")]
    public async Task<IActionResult> Menu(string handle, CancellationToken ct)
        => Ok(ApiResponse<MenuDto>.Ok(await nav.GetMenuAsync(handle, ct)));

    [HttpPut("menus/{handle}")]
    public async Task<IActionResult> SaveMenu(string handle, SaveMenuRequest req, CancellationToken ct)
        => Ok(ApiResponse<MenuDto>.Ok(await nav.SaveMenuAsync(handle, req, ct), "Menu saved."));

    [HttpGet("redirects")]
    public async Task<IActionResult> Redirects(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<RedirectDto>>.Ok(await nav.ListRedirectsAsync(ct)));

    [HttpPost("redirects")]
    public async Task<IActionResult> CreateRedirect(SaveRedirectRequest req, CancellationToken ct)
        => Ok(ApiResponse<RedirectDto>.Ok(await nav.CreateRedirectAsync(req, ct), "Redirect added."));

    [HttpPut("redirects/{id:long}")]
    public async Task<IActionResult> UpdateRedirect(long id, SaveRedirectRequest req, CancellationToken ct)
        => Ok(ApiResponse<RedirectDto>.Ok(await nav.UpdateRedirectAsync(id, req, ct), "Redirect saved."));

    [HttpDelete("redirects/{id:long}")]
    public async Task<IActionResult> DeleteRedirect(long id, CancellationToken ct)
    { await nav.DeleteRedirectAsync(id, ct); return Ok(ApiResponse<object>.Ok(new { }, "Redirect deleted.")); }
}

/// <summary>Public: storefront menu by handle + redirect lookup.</summary>
[ApiController]
[Route("api/catalog/navigation")]
public sealed class NavigationPublicController(INavigationService nav) : ControllerBase
{
    [HttpGet("menus/{handle}")]
    public async Task<IActionResult> Menu(string handle, CancellationToken ct)
        => Ok(ApiResponse<MenuDto>.Ok(await nav.GetMenuAsync(handle, ct)));

    [HttpGet("redirect")]
    public async Task<IActionResult> Redirect([FromQuery] string path, CancellationToken ct)
        => Ok(ApiResponse<object>.Ok(new { to = await nav.ResolveRedirectAsync(path, ct) }));
}
