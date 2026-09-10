using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Catalog;

[ApiController]
[Route("api/admin/products/{productId:long}/custom-text-fields")]
[Authorize(Roles = "Admin")]
public sealed class ProductCustomTextFieldsAdminController : ControllerBase
{
    private readonly ICustomTextFieldService _fields;

    public ProductCustomTextFieldsAdminController(ICustomTextFieldService fields) => _fields = fields;

    [HttpGet]
    public async Task<IActionResult> Get(long productId, CancellationToken ct)
    {
        var dto = await _fields.GetAsync(productId, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Product not found.")) : Ok(ApiResponse<List<ProductCustomTextFieldDto>>.Ok(dto));
    }

    [HttpPut]
    public async Task<IActionResult> Set(long productId, SetCustomTextFieldsRequest request, CancellationToken ct)
    {
        var dto = await _fields.SetAsync(productId, request, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Product not found.")) : Ok(ApiResponse<List<ProductCustomTextFieldDto>>.Ok(dto, "Custom text fields saved."));
    }
}
