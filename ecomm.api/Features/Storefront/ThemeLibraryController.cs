using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Storefront;

/// <summary>Merchant theme library (S5): manage multiple themes; exactly one is Published (live).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/themes")]
public sealed class ThemeLibraryController(IThemeLibraryService library) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ThemeSummaryDto>>.Ok(await library.ListAsync(ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Ok(ApiResponse<ThemeSummaryDto>.Ok(await library.GetAsync(id, ct)));

    [HttpPost]
    public async Task<IActionResult> Create(CreateThemeRequest req, CancellationToken ct)
        => Ok(ApiResponse<ThemeSummaryDto>.Ok(await library.CreateAsync(req.Name, ct), "Theme created."));

    [HttpPost("{id:long}/duplicate")]
    public async Task<IActionResult> Duplicate(long id, DuplicateThemeRequest req, CancellationToken ct)
        => Ok(ApiResponse<ThemeSummaryDto>.Ok(await library.DuplicateAsync(id, req.Name, ct), "Theme duplicated."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Rename(long id, RenameThemeRequest req, CancellationToken ct)
        => Ok(ApiResponse<ThemeSummaryDto>.Ok(await library.RenameAsync(id, req.Name, ct), "Theme renamed."));

    [HttpPost("{id:long}/publish")]
    public async Task<IActionResult> Publish(long id, CancellationToken ct)
    {
        await library.PublishAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Theme published."));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await library.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Theme deleted."));
    }
}
