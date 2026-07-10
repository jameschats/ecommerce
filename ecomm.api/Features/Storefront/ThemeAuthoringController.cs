using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Storefront;

/// <summary>Merchant theme editor (S4): author the section list of each page-type template + zone.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/theme")]
public sealed class ThemeAuthoringController(IThemeAuthoringService authoring) : ControllerBase
{
    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ThemeTemplateSummaryDto>>.Ok(await authoring.ListTemplatesAsync(ct)));

    [HttpGet("templates/{key}/sections")]
    public async Task<IActionResult> Sections(string key, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ThemeSectionAdminDto>>.Ok(await authoring.GetSectionsAsync(key, ct)));

    [HttpPost("templates/{key}/sections")]
    public async Task<IActionResult> Add(string key, AddThemeSectionRequest req, CancellationToken ct)
        => Ok(ApiResponse<ThemeSectionAdminDto>.Ok(await authoring.AddSectionAsync(key, req.SectionType, ct), "Section added."));

    [HttpPut("templates/{key}/reorder")]
    public async Task<IActionResult> Reorder(string key, ReorderThemeSectionsRequest req, CancellationToken ct)
    {
        await authoring.ReorderSectionsAsync(key, req.OrderedSectionIds, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Reordered."));
    }

    [HttpPut("sections/{id:long}")]
    public async Task<IActionResult> Update(long id, SaveThemeSectionRequest req, CancellationToken ct)
        => Ok(ApiResponse<ThemeSectionAdminDto>.Ok(await authoring.UpdateSectionAsync(id, req, ct), "Section saved."));

    [HttpPost("sections/{id:long}/duplicate")]
    public async Task<IActionResult> Duplicate(long id, CancellationToken ct)
        => Ok(ApiResponse<ThemeSectionAdminDto>.Ok(await authoring.DuplicateSectionAsync(id, ct), "Section duplicated."));

    [HttpDelete("sections/{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await authoring.DeleteSectionAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Section removed."));
    }
}
