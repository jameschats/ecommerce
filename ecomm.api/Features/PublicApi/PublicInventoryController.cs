using ecomm.api.Common.Models;
using ecomm.api.Features.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ecomm.api.Features.PublicApi;

/// <summary>The one public resource with a write scope in v1 — stock sync is a common enough real
/// integration need (multi-channel inventory tools) to justify it, per the plan's own design intent
/// to exercise real read+write, not just read-only, before anything third-party depends on this.</summary>
[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthDefaults.Scheme)]
[EnableRateLimiting("public-api")]
[Route("api/public/v1/inventory")]
public sealed class PublicInventoryController(IInventoryService inventory) : ControllerBase
{
    [HttpGet]
    [RequiresScope("inventory:read")]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await inventory.ListAsync(new InventoryQuery(Search: null, LowStockOnly: false, Page: page, PageSize: Math.Clamp(pageSize, 1, 100)), ct);
        var items = result.Items.Select(Map).ToList();
        return Ok(ApiResponse<PagedResult<PublicInventoryDto>>.Ok(new PagedResult<PublicInventoryDto>
        {
            Items = items, Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount,
        }));
    }

    [HttpGet("{productId:long}")]
    [RequiresScope("inventory:read")]
    public async Task<IActionResult> Get(long productId, CancellationToken ct)
    {
        // IInventoryService has no "get by product id" read today — only ListAsync/LowStockAsync.
        // Loads every row to filter in memory, which is a real cost on a very large catalog; worth
        // adding a dedicated single-row read to IInventoryService if this endpoint sees real traffic.
        var current = await inventory.ListAsync(new InventoryQuery(Search: null, LowStockOnly: false, Page: 1, PageSize: int.MaxValue), ct);
        var row = current.Items.FirstOrDefault(r => r.ProductId == productId);
        if (row is null) return NotFound(ApiResponse<object>.Fail("Product not found in inventory."));
        return Ok(ApiResponse<PublicInventoryDto>.Ok(Map(row)));
    }

    [HttpPut("{productId:long}")]
    [RequiresScope("inventory:write")]
    public async Task<IActionResult> Update(long productId, UpdateInventoryRequest request, CancellationToken ct)
    {
        var row = await inventory.SetStockAsync(productId, new SetStockRequest(request.AvailableQty, request.ReorderLevel), userId: null, ct);
        if (row is null) return NotFound(ApiResponse<object>.Fail("Product not found."));
        return Ok(ApiResponse<PublicInventoryDto>.Ok(Map(row), "Stock updated."));
    }

    private static PublicInventoryDto Map(InventoryRowDto r) => new(r.ProductId, r.Sku, r.AvailableQty, r.ReservedQty, r.ReorderLevel, r.IsLowStock);
}
