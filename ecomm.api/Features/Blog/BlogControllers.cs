using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ecomm.api.Features.Blog;

/// <summary>Admin blog management. CRUD is available on any plan; the AI first-draft needs the growth plan + credits.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/articles")]
public sealed class ArticleAdminController(IArticleService articles) : ControllerBase
{
    private long? UserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<ArticleSummaryDto>>.Ok(await articles.ListAsync(page, pageSize, ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Ok(ApiResponse<ArticleDto>.Ok(await articles.GetAsync(id, ct)));

    [HttpPost]
    public async Task<IActionResult> Create(SaveArticleRequest request, CancellationToken ct)
        => Ok(ApiResponse<ArticleDto>.Ok(await articles.CreateAsync(request, UserId, ct), "Article created."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SaveArticleRequest request, CancellationToken ct)
        => Ok(ApiResponse<ArticleDto>.Ok(await articles.UpdateAsync(id, request, ct), "Saved."));

    [HttpPost("{id:long}/publish")]
    public async Task<IActionResult> Publish(long id, [FromQuery] bool published = true, CancellationToken ct = default)
        => Ok(ApiResponse<ArticleDto>.Ok(await articles.SetPublishedAsync(id, published, ct), published ? "Published." : "Unpublished."));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await articles.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Deleted."));
    }

    /// <summary>AI first-draft of an article from a topic. Gated behind the growth plan; metered.</summary>
    [HttpPost("draft")]
    [RequiresFeature("growth")]
    public async Task<IActionResult> Draft(ArticleDraftRequest request, CancellationToken ct)
        => Ok(ApiResponse<ArticleDraftDto>.Ok(await articles.DraftAsync(request, ct), "Draft ready."));
}

/// <summary>Public storefront blog — published articles only.</summary>
[ApiController]
[Route("api/blog")]
public sealed class BlogController(IArticleService articles) : ControllerBase
{
    [HttpGet]
    [OutputCache(PolicyName = "public")]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 12, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<ArticleSummaryDto>>.Ok(await articles.PublicListAsync(page, pageSize, ct)));

    [HttpGet("{slug}")]
    [OutputCache(PolicyName = "public")]
    public async Task<IActionResult> BySlug(string slug, CancellationToken ct)
    {
        var a = await articles.PublicGetBySlugAsync(slug, ct);
        return a is null
            ? NotFound(ApiResponse<ArticleDto>.Fail("Article not found."))
            : Ok(ApiResponse<ArticleDto>.Ok(a));
    }
}
