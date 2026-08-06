using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Catalog;

[ApiController]
[Route("api/admin/attributes")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.CatalogManage)]
public sealed class AttributesAdminController : ControllerBase
{
    private readonly IAttributeService _attributes;

    public AttributesAdminController(IAttributeService attributes) => _attributes = attributes;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<AttributeDto>>.Ok(await _attributes.ListAsync(ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var dto = await _attributes.GetAsync(id, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Attribute not found.")) : Ok(ApiResponse<AttributeDto>.Ok(dto));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveAttributeRequest request, CancellationToken ct)
        => Ok(ApiResponse<AttributeDto>.Ok(await _attributes.CreateAsync(request, ct), "Attribute created."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SaveAttributeRequest request, CancellationToken ct)
    {
        var dto = await _attributes.UpdateAsync(id, request, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Attribute not found.")) : Ok(ApiResponse<AttributeDto>.Ok(dto, "Attribute updated."));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var ok = await _attributes.DeleteAsync(id, ct);
        return ok ? Ok(ApiResponse<object>.Ok(new { deleted = true })) : NotFound(ApiResponse<object>.Fail("Attribute not found."));
    }

    [HttpPost("{id:long}/values")]
    public async Task<IActionResult> AddValue(long id, SaveAttributeValueRequest request, CancellationToken ct)
    {
        var dto = await _attributes.AddValueAsync(id, request, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Attribute not found.")) : Ok(ApiResponse<AttributeValueDto>.Ok(dto, "Value added."));
    }

    [HttpDelete("{id:long}/values/{valueId:long}")]
    public async Task<IActionResult> DeleteValue(long id, long valueId, CancellationToken ct)
    {
        var ok = await _attributes.DeleteValueAsync(id, valueId, ct);
        return ok ? Ok(ApiResponse<object>.Ok(new { deleted = true })) : NotFound(ApiResponse<object>.Fail("Value not found."));
    }
}
