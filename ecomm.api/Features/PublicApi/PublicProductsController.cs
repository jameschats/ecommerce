using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ecomm.api.Features.PublicApi;

/// <summary>Public API (v4 Phase 6 Track A) — a deliberately separate, versioned surface for
/// third-party integrators. Never the internal admin API the Angular app calls.</summary>
[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthDefaults.Scheme)]
[EnableRateLimiting("public-api")]
[Route("api/public/v1/products")]
public sealed class PublicProductsController(IProductService products) : ControllerBase
{
    [HttpGet]
    [RequiresScope("products:read")]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var query = new ProductQuery(Search: null, CategoryId: null, BrandId: null, Status: "Active",
            IsFeatured: null, Sort: null, Page: page, PageSize: Math.Clamp(pageSize, 1, 100));
        var result = await products.BrowseAsync(query, adminView: false, ct);
        var items = result.Items.Select(Map).ToList();
        return Ok(ApiResponse<PagedResult<PublicProductDto>>.Ok(new PagedResult<PublicProductDto>
        {
            Items = items, Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount,
        }));
    }

    [HttpGet("{id:long}")]
    [RequiresScope("products:read")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var p = await products.GetByIdAsync(id, ct);
        if (p is null) return NotFound(ApiResponse<object>.Fail("Product not found."));
        return Ok(ApiResponse<PublicProductDto>.Ok(Map(p)));
    }

    private static PublicProductDto Map(ProductListItemDto p) => new(
        p.ProductId, p.Sku, p.Name, p.Slug, p.Price, p.CompareAtPrice, p.Status, p.InStock, p.AvailableQty,
        p.CategoryName, p.BrandName, new[] { p.PrimaryImageUrl, p.SecondaryImageUrl }.Where(u => u is not null).Cast<string>().ToList());

    private static PublicProductDto Map(ProductDetailDto p) => new(
        p.ProductId, p.Sku, p.Name, p.Slug, p.Price, p.CompareAtPrice, p.Status, p.InStock, p.AvailableQty,
        p.CategoryName, p.BrandName, p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).ToList());
}
