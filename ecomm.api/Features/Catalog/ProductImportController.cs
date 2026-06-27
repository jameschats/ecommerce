using System.Security.Claims;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Catalog;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "Admin")]
public sealed class ProductImportController : ControllerBase
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IProductImportService _import;

    public ProductImportController(IProductImportService import) => _import = import;

    private long? CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
        => File(await _import.ExportAsync(ct), XlsxMime, "products.xlsx");

    [HttpGet("import-template")]
    public IActionResult Template()
        => File(_import.Template(), XlsxMime, "products-template.xlsx");

    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new AppException("Please upload a non-empty .xlsx file.");
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new AppException("Only .xlsx files are supported.");

        await using var stream = file.OpenReadStream();
        var result = await _import.ImportAsync(stream, file.FileName, CurrentUserId, ct);
        return Ok(ApiResponse<ImportResultDto>.Ok(result, $"Imported {result.Job.SuccessRows}/{result.Job.TotalRows} rows."));
    }
}
