using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Cart;

[ApiController]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private const string CartTokenHeader = "X-Cart-Token";
    private readonly ICartService _cart;
    public CartController(ICartService cart) => _cart = cart;

    private long? CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    private string? CartToken =>
        Request.Headers.TryGetValue(CartTokenHeader, out var v) ? v.ToString() : null;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(ApiResponse<CartDto>.Ok(await _cart.GetCartAsync(CurrentUserId, CartToken, ct)));

    [HttpPost("items")]
    public async Task<IActionResult> AddItem(AddToCartRequest request, CancellationToken ct)
        => Ok(ApiResponse<CartDto>.Ok(await _cart.AddItemAsync(CurrentUserId, CartToken, request, ct), "Added to cart."));

    [HttpPut("items/{id:long}")]
    public async Task<IActionResult> UpdateItem(long id, UpdateCartItemRequest request, CancellationToken ct)
        => Ok(ApiResponse<CartDto>.Ok(await _cart.UpdateItemAsync(CurrentUserId, CartToken, id, request.Quantity, ct), "Cart updated."));

    [HttpDelete("items/{id:long}")]
    public async Task<IActionResult> RemoveItem(long id, CancellationToken ct)
        => Ok(ApiResponse<CartDto>.Ok(await _cart.RemoveItemAsync(CurrentUserId, CartToken, id, ct), "Item removed."));

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct)
        => Ok(ApiResponse<CartDto>.Ok(await _cart.ClearAsync(CurrentUserId, CartToken, ct), "Cart cleared."));

    [HttpPut("notes")]
    public async Task<IActionResult> SetNotes(SetCartNotesRequest request, CancellationToken ct)
        => Ok(ApiResponse<CartDto>.Ok(await _cart.SetNotesAsync(CurrentUserId, CartToken, request.Notes, ct), "Note saved."));

    [Authorize]
    [HttpPost("merge")]
    public async Task<IActionResult> Merge(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Unauthorized(ApiResponse<object>.Fail("Not authenticated."));
        return Ok(ApiResponse<CartDto>.Ok(await _cart.MergeAsync(userId.Value, CartToken, ct), "Cart merged."));
    }
}
