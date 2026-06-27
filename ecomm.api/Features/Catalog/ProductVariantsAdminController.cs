using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Catalog;

[ApiController]
[Route("api/admin/products/{productId:long}/variants")]
[Authorize(Roles = "Admin")]
public sealed class ProductVariantsAdminController : ControllerBase
{
    private readonly IVariantService _variants;

    public ProductVariantsAdminController(IVariantService variants) => _variants = variants;

    [HttpGet]
    public async Task<IActionResult> List(long productId, CancellationToken ct)
        => Ok(ApiResponse<List<ProductVariantDto>>.Ok(await _variants.ListAsync(productId, ct)));

    [HttpPost]
    public async Task<IActionResult> Create(long productId, SaveVariantRequest request, CancellationToken ct)
    {
        var dto = await _variants.CreateAsync(productId, request, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Product not found.")) : Ok(ApiResponse<ProductVariantDto>.Ok(dto, "Variant created."));
    }

    [HttpPut("{variantId:long}")]
    public async Task<IActionResult> Update(long productId, long variantId, SaveVariantRequest request, CancellationToken ct)
    {
        var dto = await _variants.UpdateAsync(productId, variantId, request, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Variant not found.")) : Ok(ApiResponse<ProductVariantDto>.Ok(dto, "Variant updated."));
    }

    [HttpDelete("{variantId:long}")]
    public async Task<IActionResult> Delete(long productId, long variantId, CancellationToken ct)
    {
        var ok = await _variants.DeleteAsync(productId, variantId, ct);
        return ok ? Ok(ApiResponse<object>.Ok(new { deleted = true })) : NotFound(ApiResponse<object>.Fail("Variant not found."));
    }
}
