using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.ColorSwatches;

/// <summary>Admin: manage the tenant's colour name -&gt; hex swatch dictionary (migration 255).</summary>
[ApiController]
[Route("api/admin/color-swatches")]
[Authorize(Roles = "Admin")]
public sealed class ColorSwatchController : ControllerBase
{
    private readonly IColorSwatchService _swatches;
    public ColorSwatchController(IColorSwatchService swatches) => _swatches = swatches;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<ColorSwatchDto>>.Ok(await _swatches.ListAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create(SaveColorSwatchRequest req, CancellationToken ct)
        => Ok(ApiResponse<ColorSwatchDto>.Ok(await _swatches.CreateAsync(req, ct), "Colour swatch created."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SaveColorSwatchRequest req, CancellationToken ct)
        => Ok(ApiResponse<ColorSwatchDto>.Ok(await _swatches.UpdateAsync(id, req, ct), "Colour swatch updated."));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _swatches.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Colour swatch deleted."));
    }
}
