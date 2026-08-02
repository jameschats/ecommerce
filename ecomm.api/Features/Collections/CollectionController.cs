using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Collections;

public sealed record SetMembersRequest(List<long> ProductIds);

/// <summary>Merchant-admin Collections CRUD + membership.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/collections")]
public sealed class CollectionAdminController(ICollectionService collections) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<CollectionDto>>.Ok(await collections.ListAsync(ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Ok(ApiResponse<CollectionDto>.Ok(await collections.GetAsync(id, ct)));

    [HttpGet("{id:long}/products")]
    public async Task<IActionResult> Members(long id, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<CollectionProductDto>>.Ok(await collections.MembersAsync(id, activeOnly: false, ct)));

    [HttpPost]
    public async Task<IActionResult> Create(SaveCollectionRequest req, CancellationToken ct)
        => Ok(ApiResponse<CollectionDto>.Ok(await collections.CreateAsync(req, ct), "Collection created."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SaveCollectionRequest req, CancellationToken ct)
        => Ok(ApiResponse<CollectionDto>.Ok(await collections.UpdateAsync(id, req, ct), "Collection saved."));

    [HttpPut("{id:long}/products")]
    public async Task<IActionResult> SetMembers(long id, SetMembersRequest req, CancellationToken ct)
    { await collections.SetManualMembersAsync(id, req.ProductIds ?? [], ct); return Ok(ApiResponse<object>.Ok(new { }, "Products updated.")); }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    { await collections.DeleteAsync(id, ct); return Ok(ApiResponse<object>.Ok(new { }, "Collection deleted.")); }
}

/// <summary>Public: a collection + its products for the storefront.</summary>
[ApiController]
[Route("api/catalog/collections")]
public sealed class CollectionPublicController(ICollectionService collections) : ControllerBase
{
    /// <summary>All active collections, lightweight — for the "all collections" index page/section.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<PublicCollectionSummaryDto>>.Ok(await collections.ListPublicAsync(ct)));

    [HttpGet("{slug}")]
    public async Task<IActionResult> BySlug(string slug, CancellationToken ct)
    {
        var c = await collections.GetBySlugAsync(slug, ct);
        return c is null ? NotFound(ApiResponse<object>.Fail("Collection not found.")) : Ok(ApiResponse<PublicCollectionDto>.Ok(c));
    }

    /// <summary>Rich-shape (swatches/stock) product list for a theme section sourcing from this
    /// collection by id — e.g. FeaturedProducts with source=collection.</summary>
    [HttpGet("{id:long}/members")]
    public async Task<IActionResult> Members(long id, [FromQuery] int limit, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ecomm.api.Features.Catalog.Dtos.ProductListItemDto>>.Ok(
            await collections.MembersForStorefrontAsync(id, limit <= 0 ? 8 : limit, ct)));
}
