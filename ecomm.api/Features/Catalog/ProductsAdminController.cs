using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Catalog;

[ApiController]
[Route("api/admin/products")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.CatalogManage)]
public sealed class ProductsAdminController : ControllerBase
{
    private readonly IProductService _products;

    public ProductsAdminController(IProductService products) => _products = products;

    private long? CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ProductQuery query, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<ProductListItemDto>>.Ok(await _products.BrowseAsync(query, adminView: true, ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var dto = await _products.GetByIdAsync(id, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Product not found.")) : Ok(ApiResponse<ProductDetailDto>.Ok(dto));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveProductRequest request, CancellationToken ct)
        => Ok(ApiResponse<ProductDetailDto>.Ok(await _products.CreateAsync(request, CurrentUserId, ct), "Product created."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SaveProductRequest request, CancellationToken ct)
    {
        var dto = await _products.UpdateAsync(id, request, CurrentUserId, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Product not found.")) : Ok(ApiResponse<ProductDetailDto>.Ok(dto, "Product updated."));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var ok = await _products.DeleteAsync(id, ct);
        return ok ? Ok(ApiResponse<object>.Ok(new { deleted = true })) : NotFound(ApiResponse<object>.Fail("Product not found."));
    }

    /// <summary>
    /// One action applied to many products — delete, status, category, or a price/cost
    /// revision. Delete here is soft, exactly as the single-product delete is.
    /// </summary>
    [HttpPost("bulk")]
    public async Task<IActionResult> Bulk([FromBody] BulkProductActionRequest req, CancellationToken ct)
    {
        var result = await _products.BulkAsync(req, ct);
        return Ok(ApiResponse<BulkProductActionResult>.Ok(result, result.Summary));
    }
}
