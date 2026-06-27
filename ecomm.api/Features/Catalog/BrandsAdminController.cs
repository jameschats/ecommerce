using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Catalog;

[ApiController]
[Route("api/admin/brands")]
[Authorize(Roles = "Admin")]
public sealed class BrandsAdminController : ControllerBase
{
    private readonly IBrandService _brands;

    public BrandsAdminController(IBrandService brands) => _brands = brands;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<BrandDto>>.Ok(await _brands.GetAllAsync(activeOnly: false, ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var dto = await _brands.GetAsync(id, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Brand not found.")) : Ok(ApiResponse<BrandDto>.Ok(dto));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveBrandRequest request, CancellationToken ct)
        => Ok(ApiResponse<BrandDto>.Ok(await _brands.CreateAsync(request, ct), "Brand created."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SaveBrandRequest request, CancellationToken ct)
    {
        var dto = await _brands.UpdateAsync(id, request, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Brand not found.")) : Ok(ApiResponse<BrandDto>.Ok(dto, "Brand updated."));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var ok = await _brands.DeleteAsync(id, ct);
        return ok ? Ok(ApiResponse<object>.Ok(new { deleted = true })) : NotFound(ApiResponse<object>.Fail("Brand not found."));
    }
}
