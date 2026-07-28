using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using ecomm.api.Features.ColorSwatches;
using ecomm.api.Features.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ecomm.api.Features.Catalog;

/// <summary>Public storefront browsing endpoints. Anonymous reads are output-cached
/// (60s, varied by query); authenticated requests bypass the cache automatically.</summary>
[ApiController]
[Route("api/catalog")]
[OutputCache(PolicyName = "public")]
public sealed class CatalogController : ControllerBase
{
    private readonly ICategoryService _categories;
    private readonly IBrandService _brands;
    private readonly IProductService _products;
    private readonly ISearchService _search;
    private readonly IColorSwatchService _swatches;

    public CatalogController(ICategoryService categories, IBrandService brands, IProductService products, ISearchService search, IColorSwatchService swatches)
    {
        _categories = categories;
        _brands = brands;
        _products = products;
        _search = search;
        _swatches = swatches;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct)
        => Ok(ApiResponse<List<CategoryDto>>.Ok(await _categories.GetAllAsync(activeOnly: true, ct)));

    /// <summary>Colour name -&gt; hex lookup, used by the storefront to render swatch dots for
    /// free-text variant colour values.</summary>
    [HttpGet("color-swatches")]
    public async Task<IActionResult> ColorSwatches(CancellationToken ct)
        => Ok(ApiResponse<List<ColorSwatchDto>>.Ok(await _swatches.ListAsync(ct)));

    [HttpGet("brands")]
    public async Task<IActionResult> Brands(CancellationToken ct)
        => Ok(ApiResponse<List<BrandDto>>.Ok(await _brands.GetAllAsync(activeOnly: true, ct)));

    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] ProductQuery query, CancellationToken ct)
    {
        var result = await _products.BrowseAsync(query, adminView: false, ct);
        if (!string.IsNullOrWhiteSpace(query.Search))
            await _search.LogAsync(query.Search!, (int)result.TotalCount, null, ct);
        return Ok(ApiResponse<PagedResult<ProductListItemDto>>.Ok(result));
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string q, CancellationToken ct)
        => Ok(ApiResponse<List<string>>.Ok(await _search.SuggestAsync(q ?? string.Empty, ct)));

    [HttpGet("popular-searches")]
    public async Task<IActionResult> PopularSearches(CancellationToken ct)
        => Ok(ApiResponse<List<PopularTermDto>>.Ok(await _search.PopularAsync(8, ct)));

    [HttpGet("products/{id:long}")]
    public async Task<IActionResult> Product(long id, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(id, ct);
        return product is null
            ? NotFound(ApiResponse<object>.Fail("Product not found."))
            : Ok(ApiResponse<ProductDetailDto>.Ok(product));
    }

    [HttpGet("products/by-slug/{slug}")]
    public async Task<IActionResult> ProductBySlug(string slug, CancellationToken ct)
    {
        var product = await _products.GetBySlugAsync(slug, ct);
        return product is null
            ? NotFound(ApiResponse<object>.Fail("Product not found."))
            : Ok(ApiResponse<ProductDetailDto>.Ok(product));
    }
}
