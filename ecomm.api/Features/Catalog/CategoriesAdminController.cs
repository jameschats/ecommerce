using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Catalog;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.CatalogManage)]
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

    /// <summary>
    /// What a cascade delete would destroy. Read first, so the confirmation can say the
    /// numbers rather than ask people to trust a Yes button.
    /// </summary>
    [HttpGet("{id:long}/delete-impact")]
    public async Task<IActionResult> DeleteImpact(
        long id, [FromServices] ICategoryDeletionService deletion, CancellationToken ct)
        => Ok(ApiResponse<CategoryDeleteImpact>.Ok(await deletion.ImpactAsync(id, ct)));

    /// <summary>
    /// Deletes the category along with its products and every order that touches them —
    /// invoices, payments and shipments included, through the database's own cascades.
    ///
    /// Requires the category name to be typed back, and settings.manage rather than the
    /// catalog.manage that governs the rest of this controller: removing trading records is
    /// not the same authority as editing a catalogue, and the Catalogue manager role should
    /// not carry it.
    /// </summary>
    [HttpDelete("{id:long}/cascade")]
    [Authorize(Policy = ecomm.api.Common.Security.Perm.SettingsManage)]
    public async Task<IActionResult> CascadeDelete(
        long id, [FromQuery] string confirm,
        [FromServices] ICategoryDeletionService deletion, CancellationToken ct)
    {
        var impact = await deletion.CascadeDeleteAsync(id, confirm, ct);
        return Ok(ApiResponse<CategoryDeleteImpact>.Ok(
            impact,
            $"Deleted \"{impact.Name}\" with {impact.LiveProducts + impact.DeletedProducts} products, "
            + $"{impact.Orders} orders, {impact.Invoices} invoices and {impact.Payments} payments."));
    }
}
