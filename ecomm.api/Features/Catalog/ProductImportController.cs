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

    /// <summary>
    /// Dry run — reports what the file would do without writing anything. A 400-row import
    /// committed unseen is a genuinely dangerous button (design.md §10.1).
    /// </summary>
    [HttpPost("import/preview")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Preview(IFormFile? file, CancellationToken ct)
    {
        Validate(file);
        await using var stream = file!.OpenReadStream();
        return Ok(ApiResponse<ImportPreviewDto>.Ok(await _import.PreviewAsync(stream, file.FileName, ct)));
    }

    [HttpPost("import")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken ct)
    {
        Validate(file);
        await using var stream = file!.OpenReadStream();
        var result = await _import.ImportAsync(stream, file.FileName, CurrentUserId, ct);
        return Ok(ApiResponse<ImportResultDto>.Ok(result, $"Imported {result.Job.SuccessRows}/{result.Job.TotalRows} rows."));
    }

    /// <summary>Both import endpoints accept the same formats, so the check lives in one place.</summary>
    private static void Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            throw new AppException("Please upload a non-empty .xlsx or .csv file.");

        var ok = file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
              || file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        if (!ok) throw new AppException("Only .xlsx and .csv files are supported.");
    }

    /// <summary>
    /// Bulk product images from a ZIP, matched by Design No (design.md §10.2).
    /// Files are named DESIGNNO.jpg, or DESIGNNO-1.jpg / DESIGNNO-2.jpg for several.
    /// </summary>
    [HttpPost("images/zip")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<IActionResult> ImportImages(
        IFormFile? file,
        [FromQuery] bool replaceExisting,
        [FromServices] IProductImageZipService zip,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new AppException("Please upload a non-empty .zip file.");
        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new AppException("Only .zip files are supported.");

        await using var stream = file.OpenReadStream();
        var result = await zip.ImportAsync(stream, replaceExisting, ct);

        return Ok(ApiResponse<ZipImageResultDto>.Ok(
            result,
            $"{result.Matched} image(s) attached to {result.ProductsUpdated} product(s)."
            + (result.Unmatched.Count > 0 ? $" {result.Unmatched.Count} file(s) did not match any design number." : "")));
    }
}
