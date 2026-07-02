using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Suppliers;

/// <summary>Admin: manage suppliers + assign them to products (for cost sourcing / profit-by-supplier).</summary>
[ApiController]
[Route("api/admin/suppliers")]
[Authorize(Roles = "Admin")]
public sealed class SupplierController : ControllerBase
{
    private readonly ISupplierService _suppliers;
    public SupplierController(ISupplierService suppliers) => _suppliers = suppliers;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<SupplierDto>>.Ok(await _suppliers.ListAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create(SaveSupplierRequest req, CancellationToken ct)
        => Ok(ApiResponse<SupplierDto>.Ok(await _suppliers.CreateAsync(req, ct), "Supplier created."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SaveSupplierRequest req, CancellationToken ct)
        => Ok(ApiResponse<SupplierDto>.Ok(await _suppliers.UpdateAsync(id, req, ct), "Supplier updated."));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _suppliers.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Supplier deleted."));
    }
}

/// <summary>Admin: the supplier links for a product (SKU, cost, primary).</summary>
[ApiController]
[Route("api/admin/products/{productId:long}/suppliers")]
[Authorize(Roles = "Admin")]
public sealed class ProductSuppliersController : ControllerBase
{
    private readonly ISupplierService _suppliers;
    public ProductSuppliersController(ISupplierService suppliers) => _suppliers = suppliers;

    public sealed record SetProductSuppliersRequest(List<ProductSupplierInput> Suppliers);

    [HttpGet]
    public async Task<IActionResult> Get(long productId, CancellationToken ct)
        => Ok(ApiResponse<List<ProductSupplierDto>>.Ok(await _suppliers.GetForProductAsync(productId, ct)));

    [HttpPut]
    public async Task<IActionResult> Set(long productId, SetProductSuppliersRequest req, CancellationToken ct)
        => Ok(ApiResponse<List<ProductSupplierDto>>.Ok(await _suppliers.SetForProductAsync(productId, req.Suppliers ?? [], ct), "Suppliers updated."));
}
