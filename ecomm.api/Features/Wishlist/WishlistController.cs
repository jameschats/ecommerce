using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Wishlist;

/// <summary>Customer wishlist / save-for-later.</summary>
[ApiController]
[Route("api/wishlist")]
[Authorize]
public sealed class WishlistController : ControllerBase
{
    private readonly IWishlistService _wishlist;
    public WishlistController(IWishlistService wishlist) => _wishlist = wishlist;

    private long CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<ProductListItemDto>>.Ok(await _wishlist.GetAsync(CurrentUserId, ct)));

    [HttpGet("ids")]
    public async Task<IActionResult> Ids(CancellationToken ct)
        => Ok(ApiResponse<List<long>>.Ok(await _wishlist.GetProductIdsAsync(CurrentUserId, ct)));

    [HttpPost("{productId:long}")]
    public async Task<IActionResult> Add(long productId, CancellationToken ct)
    {
        await _wishlist.AddAsync(CurrentUserId, productId, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Added to wishlist."));
    }

    [HttpDelete("{productId:long}")]
    public async Task<IActionResult> Remove(long productId, CancellationToken ct)
    {
        await _wishlist.RemoveAsync(CurrentUserId, productId, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Removed from wishlist."));
    }
}
