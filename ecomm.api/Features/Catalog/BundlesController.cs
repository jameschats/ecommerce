using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Catalog;

/// <summary>Admin management of a bundle product's components (Phase H). A bundle is a normal
/// Product (IsBundle=true) — this only manages what real products/quantities it expands to at checkout.</summary>
[ApiController]
[Route("api/admin/products/{productId:long}/bundle-items")]
[Authorize(Roles = "Admin")]
public sealed class BundlesAdminController : ControllerBase
{
    private readonly IBundleService _bundles;
    public BundlesAdminController(IBundleService bundles) => _bundles = bundles;

    [HttpGet]
    public async Task<IActionResult> Get(long productId, CancellationToken ct)
        => Ok(ApiResponse<List<BundleComponentDto>>.Ok(await _bundles.ComponentsAsync(productId, ct)));

    [HttpPut]
    public async Task<IActionResult> Save(long productId, [FromBody] List<BundleItemInput> items, CancellationToken ct)
        => Ok(ApiResponse<List<BundleComponentDto>>.Ok(await _bundles.SaveComponentsAsync(productId, items, ct), "Bundle contents saved."));
}

/// <summary>Public, storefront-facing read of a bundle's contents (for the PDP "Includes:" list).</summary>
[ApiController]
[Route("api/catalog/products/{productId:long}/bundle-items")]
public sealed class BundlesPublicController : ControllerBase
{
    private readonly IBundleService _bundles;
    public BundlesPublicController(IBundleService bundles) => _bundles = bundles;

    [HttpGet]
    public async Task<IActionResult> Get(long productId, CancellationToken ct)
        => Ok(ApiResponse<List<BundleComponentDto>>.Ok(await _bundles.ComponentsAsync(productId, ct)));
}
