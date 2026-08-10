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

    // Tagged so an edit in admin can evict it. Without that, saving a page would appear to do
    // nothing for up to a minute — which reads as a broken screen, not a cache.
    [HttpGet("{slug}")]
    [OutputCache(PolicyName = "public", Tags = ["content-pages"])]
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
    private readonly IOutputCacheStore _cache;

    public ContentPagesAdminController(IContentPageService pages, IOutputCacheStore cache)
    {
        _pages = pages;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<ContentPageDto>>.Ok(await _pages.ListAsync(ct)));

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct)
        => await FoundAsync(await _pages.GetForAdminAsync(slug, ct), null, ct);

    [HttpPut("{slug}")]
    public async Task<IActionResult> Save(string slug, [FromBody] SavePageRequest req, CancellationToken ct)
        => await FoundAsync(await _pages.SavePageAsync(slug, req, ct), "Saved.", ct);

    [HttpPost("{slug}/sections")]
    public async Task<IActionResult> AddSection(string slug, [FromBody] SaveSectionRequest req, CancellationToken ct)
        => await FoundAsync(await _pages.AddSectionAsync(slug, req, ct), "Section added.", ct);

    [HttpPut("sections/{sectionId:long}")]
    public async Task<IActionResult> UpdateSection(long sectionId, [FromBody] SaveSectionRequest req, CancellationToken ct)
        => await FoundAsync(await _pages.UpdateSectionAsync(sectionId, req, ct), "Saved.", ct);

    [HttpDelete("sections/{sectionId:long}")]
    public async Task<IActionResult> DeleteSection(long sectionId, CancellationToken ct)
        => await FoundAsync(await _pages.DeleteSectionAsync(sectionId, ct), "Section deleted.", ct);

    [HttpPut("{slug}/order")]
    public async Task<IActionResult> Reorder(string slug, [FromBody] ReorderSectionsRequest req, CancellationToken ct)
        => await FoundAsync(await _pages.ReorderAsync(slug, req.SectionIds, ct), "Order saved.", ct);

    /// <summary>
    /// Returns the page and, when something was written, drops the cached public copy so the
    /// change is live at once rather than after the 60-second window.
    /// </summary>
    private async Task<IActionResult> FoundAsync(ContentPageDto? page, string? message, CancellationToken ct)
    {
        if (page is null) return NotFound(ApiResponse<object>.Fail("Page not found."));
        if (message is not null) await _cache.EvictByTagAsync("content-pages", ct);
        return Ok(ApiResponse<ContentPageDto>.Ok(page, message));
    }
}
