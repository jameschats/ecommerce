using ecomm.api.Common.Models;
using ecomm.api.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ecomm.api.Features.Cms;

public sealed record ReorderSectionsRequest(List<long> SectionIds);

/// <summary>Public read of a content page. Anonymous — these are the storefront's own pages.</summary>
[ApiController]
[Route("api/cms/pages")]
public sealed class ContentPageController : ControllerBase
{
    private readonly IContentPageService _pages;
    public ContentPageController(IContentPageService pages) => _pages = pages;

    [HttpGet("{slug}")]
    [OutputCache(PolicyName = "public")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct)
    {
        var page = await _pages.GetPublishedAsync(slug, ct);
        return page is null
            ? NotFound(ApiResponse<object>.Fail("Page not found."))
            : Ok(ApiResponse<ContentPageDto>.Ok(page));
    }
}

[ApiController]
[Route("api/admin/cms/pages")]
[Authorize(Policy = Perm.CmsManage)]
public sealed class ContentPagesAdminController : ControllerBase
{
    private readonly IContentPageService _pages;
    public ContentPagesAdminController(IContentPageService pages) => _pages = pages;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<ContentPageDto>>.Ok(await _pages.ListAsync(ct)));

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct)
        => Found(await _pages.GetForAdminAsync(slug, ct));

    [HttpPut("{slug}")]
    public async Task<IActionResult> Save(string slug, [FromBody] SavePageRequest req, CancellationToken ct)
        => Found(await _pages.SavePageAsync(slug, req, ct), "Saved.");

    [HttpPost("{slug}/sections")]
    public async Task<IActionResult> AddSection(string slug, [FromBody] SaveSectionRequest req, CancellationToken ct)
        => Found(await _pages.AddSectionAsync(slug, req, ct), "Section added.");

    [HttpPut("sections/{sectionId:long}")]
    public async Task<IActionResult> UpdateSection(long sectionId, [FromBody] SaveSectionRequest req, CancellationToken ct)
        => Found(await _pages.UpdateSectionAsync(sectionId, req, ct), "Saved.");

    [HttpDelete("sections/{sectionId:long}")]
    public async Task<IActionResult> DeleteSection(long sectionId, CancellationToken ct)
        => Found(await _pages.DeleteSectionAsync(sectionId, ct), "Section deleted.");

    [HttpPut("{slug}/order")]
    public async Task<IActionResult> Reorder(string slug, [FromBody] ReorderSectionsRequest req, CancellationToken ct)
        => Found(await _pages.ReorderAsync(slug, req.SectionIds, ct), "Order saved.");

    private IActionResult Found(ContentPageDto? page, string? message = null)
        => page is null
            ? NotFound(ApiResponse<object>.Fail("Page not found."))
            : Ok(ApiResponse<ContentPageDto>.Ok(page, message));
}
