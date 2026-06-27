using System.Security.Claims;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Inventory;

[ApiController]
[Route("api/admin/inventory")]
[Authorize(Roles = "Admin")]
public sealed class InventoryAdminController : ControllerBase
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IInventoryService _inventory;
    private readonly IInventoryImportService _import;

    public InventoryAdminController(IInventoryService inventory, IInventoryImportService import)
    {
        _inventory = inventory;
        _import = import;
    }

    private long? CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] InventoryQuery query, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<InventoryRowDto>>.Ok(await _inventory.ListAsync(query, ct)));

    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock(CancellationToken ct)
        => Ok(ApiResponse<List<InventoryRowDto>>.Ok(await _inventory.LowStockAsync(ct)));

    [HttpGet("{productId:long}/transactions")]
    public async Task<IActionResult> Transactions(long productId, CancellationToken ct)
        => Ok(ApiResponse<List<InventoryTransactionDto>>.Ok(await _inventory.TransactionsAsync(productId, ct)));

    [HttpPut("{productId:long}")]
    public async Task<IActionResult> SetStock(long productId, SetStockRequest request, CancellationToken ct)
    {
        var row = await _inventory.SetStockAsync(productId, request, CurrentUserId, ct);
        return row is null ? NotFound(ApiResponse<object>.Fail("Product not found.")) : Ok(ApiResponse<InventoryRowDto>.Ok(row, "Stock updated."));
    }

    [HttpPost("{productId:long}/adjust")]
    public async Task<IActionResult> Adjust(long productId, AdjustStockRequest request, CancellationToken ct)
    {
        var row = await _inventory.AdjustAsync(productId, request, CurrentUserId, ct);
        return row is null ? NotFound(ApiResponse<object>.Fail("Product not found.")) : Ok(ApiResponse<InventoryRowDto>.Ok(row, "Stock adjusted."));
    }

    [HttpGet("{productId:long}/variants")]
    public async Task<IActionResult> Variants(long productId, CancellationToken ct)
        => Ok(ApiResponse<List<VariantInventoryDto>>.Ok(await _inventory.VariantInventoryAsync(productId, ct)));

    [HttpPut("{productId:long}/variant/{variantId:long}")]
    public async Task<IActionResult> SetVariantStock(long productId, long variantId, SetStockRequest request, CancellationToken ct)
    {
        var row = await _inventory.SetVariantStockAsync(productId, variantId, request, CurrentUserId, ct);
        return row is null ? NotFound(ApiResponse<object>.Fail("Variant not found.")) : Ok(ApiResponse<VariantInventoryDto>.Ok(row, "Variant stock updated."));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
        => File(await _import.ExportAsync(ct), XlsxMime, "inventory.xlsx");

    [HttpGet("import-template")]
    public IActionResult Template()
        => File(_import.Template(), XlsxMime, "inventory-template.xlsx");

    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new AppException("Please upload a non-empty .xlsx file.");
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new AppException("Only .xlsx files are supported.");

        await using var stream = file.OpenReadStream();
        var result = await _import.ImportAsync(stream, CurrentUserId, ct);
        return Ok(ApiResponse<InventoryImportResult>.Ok(result, $"Updated {result.Success}/{result.Total} rows."));
    }
}
