using ecomm.api.Common.Models;
using ecomm.api.Features.Cms.SectionTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Cms;

/// <summary>Public: storefront reads pages/sections + the section-type catalog.</summary>
[ApiController]
[Route("api/cms")]
public sealed class CmsController(ICmsService cms) : ControllerBase
{
    [HttpGet("home")]
    public async Task<IActionResult> Home(CancellationToken ct)
        => Ok(ApiResponse<List<SectionDto>>.Ok(await cms.GetHomeSectionsAsync(visibleOnly: true, ct)));

    [HttpGet("pages/{slug}")]
    public async Task<IActionResult> Page(string slug, CancellationToken ct)
    {
        var page = await cms.GetPageBySlugAsync(slug, ct);
        return page is null ? NotFound(ApiResponse<object>.Fail("Page not found.")) : Ok(ApiResponse<PageDetailDto>.Ok(page));
    }

    /// <summary>The platform's section-type catalog (drives the builder's settings forms).</summary>
    [HttpGet("section-types")]
    public IActionResult SectionTypes()
        => Ok(ApiResponse<IReadOnlyList<SectionTypeSchema>>.Ok(SectionTypeRegistry.All));

    /// <summary>Industry starter layouts a merchant can apply to a page.</summary>
    [HttpGet("presets")]
    public IActionResult Presets()
        => Ok(ApiResponse<IReadOnlyList<PresetSummary>>.Ok(cms.ListPresets()));
}

/// <summary>Admin: the visual page builder — pages + sections CRUD.</summary>
[ApiController]
[Route("api/admin/cms")]
[Authorize(Roles = "Admin")]
public sealed class CmsAdminController(ICmsService cms) : ControllerBase
{
    // --- home (back-compat with the current home-layout screen) ---
    [HttpGet("home")]
    public async Task<IActionResult> Home(CancellationToken ct)
        => Ok(ApiResponse<List<SectionDto>>.Ok(await cms.GetHomeSectionsAsync(visibleOnly: false, ct)));

    [HttpPut("home")]
    public async Task<IActionResult> UpdateHome(UpdateSectionsRequest request, CancellationToken ct)
        => Ok(ApiResponse<List<SectionDto>>.Ok(await cms.UpdateHomeSectionsAsync(request.Sections ?? [], ct), "Home layout updated."));

    // --- pages ---
    [HttpGet("pages")]
    public async Task<IActionResult> Pages(CancellationToken ct)
        => Ok(ApiResponse<List<PageDto>>.Ok(await cms.ListPagesAsync(ct)));

    [HttpGet("pages/{id:long}")]
    public async Task<IActionResult> Page(long id, CancellationToken ct)
    {
        var p = await cms.GetPageAsync(id, ct);
        return p is null ? NotFound(ApiResponse<object>.Fail("Page not found.")) : Ok(ApiResponse<PageDetailDto>.Ok(p));
    }

    [HttpPost("pages")]
    public async Task<IActionResult> CreatePage(SavePageRequest req, CancellationToken ct)
        => Ok(ApiResponse<PageDto>.Ok(await cms.CreatePageAsync(req, ct), "Page created."));

    [HttpPut("pages/{id:long}")]
    public async Task<IActionResult> UpdatePage(long id, SavePageRequest req, CancellationToken ct)
        => Ok(ApiResponse<PageDto>.Ok(await cms.UpdatePageAsync(id, req, ct), "Page saved."));

    [HttpDelete("pages/{id:long}")]
    public async Task<IActionResult> DeletePage(long id, CancellationToken ct)
    { await cms.DeletePageAsync(id, ct); return Ok(ApiResponse<object>.Ok(new { }, "Page deleted.")); }

    [HttpPut("pages/{id:long}/reorder")]
    public async Task<IActionResult> Reorder(long id, ReorderRequest req, CancellationToken ct)
    { await cms.ReorderSectionsAsync(id, req.OrderedSectionIds ?? [], ct); return Ok(ApiResponse<object>.Ok(new { }, "Reordered.")); }

    [HttpPost("pages/{id:long}/apply-preset")]
    public async Task<IActionResult> ApplyPreset(long id, ApplyPresetRequest req, CancellationToken ct)
        => Ok(ApiResponse<PageDetailDto>.Ok(await cms.ApplyPresetAsync(id, req.PresetKey ?? "", ct), "Template applied."));

    // --- sections ---
    [HttpPost("sections")]
    public async Task<IActionResult> AddSection(AddSectionRequest req, CancellationToken ct)
        => Ok(ApiResponse<SectionDto>.Ok(await cms.AddSectionAsync(req, ct), "Section added."));

    [HttpPut("sections/{id:long}")]
    public async Task<IActionResult> UpdateSection(long id, SaveSectionRequest req, CancellationToken ct)
        => Ok(ApiResponse<SectionDto>.Ok(await cms.UpdateSectionAsync(id, req, ct), "Section saved."));

    [HttpPost("sections/{id:long}/duplicate")]
    public async Task<IActionResult> DuplicateSection(long id, CancellationToken ct)
        => Ok(ApiResponse<SectionDto>.Ok(await cms.DuplicateSectionAsync(id, ct), "Section duplicated."));

    [HttpDelete("sections/{id:long}")]
    public async Task<IActionResult> DeleteSection(long id, CancellationToken ct)
    { await cms.DeleteSectionAsync(id, ct); return Ok(ApiResponse<object>.Ok(new { }, "Section deleted.")); }
}
