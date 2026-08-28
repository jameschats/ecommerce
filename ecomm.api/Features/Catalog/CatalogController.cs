using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using ecomm.api.Features.Checkout;
using ecomm.api.Features.ColorSwatches;
using ecomm.api.Features.Inventory;
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
    private readonly IShippingService _shipping;
    private readonly IBackInStockService _backInStock;

    public CatalogController(ICategoryService categories, IBrandService brands, IProductService products, ISearchService search, IColorSwatchService swatches, IShippingService shipping, IBackInStockService backInStock)
    {
        _categories = categories;
        _brands = brands;
        _products = products;
        _search = search;
        _swatches = swatches;
        _shipping = shipping;
        _backInStock = backInStock;
    }

    /// <summary>Public: register a shopper to be emailed when an out-of-stock product returns.</summary>
    [HttpPost("products/{id:long}/notify-back-in-stock")]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> NotifyBackInStock(long id, [FromBody] NotifyBackInStockRequest req, CancellationToken ct)
    {
        var userId = long.TryParse(User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (long?)null;
        await _backInStock.RequestAsync(id, req?.Email ?? "", userId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "We'll email you when it's back in stock."));
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

    /// <summary>Available filter values + counts for the current result set — drives the facet rail.</summary>
    [HttpGet("facets")]
    public async Task<IActionResult> Facets([FromQuery] ProductQuery query, CancellationToken ct)
    {
        var facets = await _products.FacetsAsync(query, ct);

        // Enrich colour values with their hex so the storefront can draw real swatch dots.
        var swatches = (await _swatches.ListAsync(ct))
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().HexCode, StringComparer.OrdinalIgnoreCase);
        if (facets.Colors.Count > 0)
        {
            var colors = facets.Colors
                .Select(c => c with { Hex = swatches.TryGetValue(c.Value, out var hex) ? hex : null })
                .ToList();
            facets = facets with { Colors = colors };
        }
        return Ok(ApiResponse<FacetsDto>.Ok(facets));
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

    /// <summary>PDP delivery/pincode checker — serviceability + estimated days for a destination
    /// pincode, ahead of checkout (no address/login required).</summary>
    [HttpGet("shipping/check")]
    public async Task<IActionResult> CheckShipping([FromQuery] string pincode, CancellationToken ct)
    {
        var pin = (pincode ?? string.Empty).Trim();
        if (pin.Length != 6 || !pin.All(char.IsDigit))
            return Ok(ApiResponse<ShippingQuote>.Fail("Enter a valid 6-digit pincode."));
        return Ok(ApiResponse<ShippingQuote>.Ok(await _shipping.QuoteAsync(pin, orderSubtotal: 0m, ct)));
    }

    [HttpGet("products/{id:long}/frequently-bought-together")]
    public async Task<IActionResult> FrequentlyBoughtTogether(long id, [FromQuery] int take, CancellationToken ct)
        => Ok(ApiResponse<List<ProductListItemDto>>.Ok(await _products.GetFrequentlyBoughtTogetherAsync(id, take <= 0 ? 3 : take, ct)));

    /// <summary>"Trending now": products ranked by recent demand velocity (views + add-to-cart + purchases).
    /// Cached for anonymous reads; empty until a store has enough recent activity.</summary>
    [HttpGet("trending")]
    [Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "public")]
    public async Task<IActionResult> Trending([FromQuery] int limit = 12, [FromQuery] int days = 7, CancellationToken ct = default)
        => Ok(ApiResponse<List<ProductListItemDto>>.Ok(await _products.GetTrendingAsync(limit <= 0 ? 12 : limit, days <= 0 ? 7 : days, ct)));

    private long? VisitorUserId =>
        long.TryParse(User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;

    /// <summary>Personalized picks for this visitor (by their first-party session id + login). Per-visitor,
    /// so never cached. Empty until the visitor has enough signal — the caller falls back to trending.</summary>
    [HttpGet("personalized")]
    public async Task<IActionResult> Personalized([FromQuery] string? sessionId, [FromQuery] int limit = 8, CancellationToken ct = default)
        => Ok(ApiResponse<List<ProductListItemDto>>.Ok(await _products.GetPersonalizedAsync(sessionId, VisitorUserId, limit <= 0 ? 8 : limit, ct)));

    /// <summary>This visitor's recently-viewed products (server-side, works across devices once logged in).</summary>
    [HttpGet("recently-viewed")]
    public async Task<IActionResult> RecentlyViewed([FromQuery] string? sessionId, [FromQuery] int limit = 8, CancellationToken ct = default)
        => Ok(ApiResponse<List<ProductListItemDto>>.Ok(await _products.GetRecentlyViewedAsync(sessionId, VisitorUserId, limit <= 0 ? 8 : limit, ct)));
}
