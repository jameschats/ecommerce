using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Catalog;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = "Admin")]
public sealed class CategoriesAdminController : ControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesAdminController(ICategoryService categories) => _categories = categories;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<CategoryDto>>.Ok(await _categories.GetAllAsync(activeOnly: false, ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var dto = await _categories.GetAsync(id, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Category not found.")) : Ok(ApiResponse<CategoryDto>.Ok(dto));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveCategoryRequest request, CancellationToken ct)
        => Ok(ApiResponse<CategoryDto>.Ok(await _categories.CreateAsync(request, ct), "Category created."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SaveCategoryRequest request, CancellationToken ct)
    {
        var dto = await _categories.UpdateAsync(id, request, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Category not found.")) : Ok(ApiResponse<CategoryDto>.Ok(dto, "Category updated."));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var ok = await _categories.DeleteAsync(id, ct);
        return ok ? Ok(ApiResponse<object>.Ok(new { deleted = true })) : NotFound(ApiResponse<object>.Fail("Category not found."));
    }
}
