using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Catalog;

[ApiController]
[Route("api/admin/products/{productId:long}/custom-fields")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.CatalogManage)]
public sealed class ProductCustomFieldsAdminController : ControllerBase
{
    private readonly IProductCustomFieldService _service;

    public ProductCustomFieldsAdminController(IProductCustomFieldService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(long productId, CancellationToken ct)
    {
        var dto = await _service.GetAsync(productId, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Product not found.")) : Ok(ApiResponse<List<ProductCustomFieldDto>>.Ok(dto));
    }

    [HttpPut]
    public async Task<IActionResult> Set(long productId, SetProductCustomFieldsRequest request, CancellationToken ct)
    {
        var dto = await _service.SetAsync(productId, request, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Product not found.")) : Ok(ApiResponse<List<ProductCustomFieldDto>>.Ok(dto, "Custom fields updated."));
    }
}
