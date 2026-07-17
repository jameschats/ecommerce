using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Ai;

/// <summary>Merchant: AI sample-catalog generator (AI-2). Generate → preview → add to store / download / clear.</summary>
[ApiController]
[Route("api/admin/ai/catalog")]
[Authorize(Roles = "Admin")]
public sealed class AiCatalogController(IAiCatalogService catalog, IAiService ai) : ControllerBase
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(ApiResponse<CatalogStatusDto>.Ok(catalog.Presets(ai.Enabled, await catalog.SampleCountAsync(ct))));

    /// <summary>Generate a preview (metered). Nothing is written until /seed is called.</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateCatalogRequest req, CancellationToken ct)
        => Ok(ApiResponse<GeneratedCatalog>.Ok(await catalog.GenerateAsync(req, ct)));

    /// <summary>Add the previewed catalog to the store (no extra credits — just writes).</summary>
    [HttpPost("seed")]
    public async Task<IActionResult> Seed([FromBody] GeneratedCatalog req, CancellationToken ct)
        => Ok(ApiResponse<SeedResultDto>.Ok(await catalog.SeedAsync(req, ct), "Sample catalog added to your store."));

    /// <summary>Remove all AI-seeded sample products (reversible clear).</summary>
    [HttpPost("clear")]
    public async Task<IActionResult> Clear(CancellationToken ct)
        => Ok(ApiResponse<object>.Ok(new { removed = await catalog.ClearAsync(ct) }, "Sample products removed."));

    /// <summary>Download the previewed catalog as an import-compatible .xlsx.</summary>
    [HttpPost("export")]
    public IActionResult Export([FromBody] GeneratedCatalog req)
        => File(catalog.BuildExcel(req), XlsxMime, "sample-catalog.xlsx");
}
