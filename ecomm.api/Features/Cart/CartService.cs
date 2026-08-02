using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;
using CartEntity = ecomm.api.Data.Entities.Cart;
using CartItemEntity = ecomm.api.Data.Entities.CartItem;

namespace ecomm.api.Features.Cart;

/// <summary>
/// Cart resolution: logged-in users own a cart by UserId; guests by a client
/// SessionId (cart token). Stock is validated against the product's total
/// available inventory (matches storefront in-stock logic); hard reservation
/// happens at checkout (Stage 5).
/// </summary>
public interface ICartService
{
    Task<CartDto> GetCartAsync(long? userId, string? sessionId, CancellationToken ct = default);
    Task<CartDto> AddItemAsync(long? userId, string? sessionId, AddToCartRequest req, CancellationToken ct = default);
    Task<CartDto> UpdateItemAsync(long? userId, string? sessionId, long itemId, int quantity, CancellationToken ct = default);
    Task<CartDto> RemoveItemAsync(long? userId, string? sessionId, long itemId, CancellationToken ct = default);
    Task<CartDto> ClearAsync(long? userId, string? sessionId, CancellationToken ct = default);
    Task<CartDto> MergeAsync(long userId, string? sessionId, CancellationToken ct = default);
    Task<CartDto> SetNotesAsync(long? userId, string? sessionId, string? notes, CancellationToken ct = default);
}

public sealed class CartService : ICartService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;
    private readonly ecomm.api.Features.Checkout.ITaxService _tax;
    private readonly ecomm.api.Features.Catalog.Services.IBundleService _bundles;
    public CartService(EcommerceDbContext db, ecomm.api.Features.Checkout.ITaxService tax, ecomm.api.Features.Catalog.Services.IBundleService bundles)
    {
        _db = db;
        _tax = tax;
        _bundles = bundles;
    }

    public async Task<CartDto> GetCartAsync(long? userId, string? sessionId, CancellationToken ct = default)
    {
        var cart = await FindCartAsync(userId, sessionId, ct);
        return await BuildDtoAsync(cart, ct);
    }

    public async Task<CartDto> AddItemAsync(long? userId, string? sessionId, AddToCartRequest req, CancellationToken ct = default)
    {
        if (req.Quantity < 1) throw new AppException("Quantity must be at least 1.");

        var product = await _db.Products.FirstOrDefaultAsync(
            p => p.ProductId == req.ProductId && p.TenantId == Tenant && !p.IsDeleted && p.IsActive && p.Status == "Active", ct)
            ?? throw new AppException("Product is not available.", 404);

        var unitPrice = product.Price;
        if (req.ProductVariantId is { } vid)
        {
            var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.ProductVariantId == vid && v.ProductId == req.ProductId, ct)
                ?? throw new AppException("Selected option is not available.", 404);
            unitPrice += variant.PriceAdjustment;
        }

        var available = await AvailableAsync(req.ProductId, ct);
        if (available <= 0) throw new AppException("This item is out of stock.");

        var cart = await GetOrCreateCartAsync(userId, sessionId, ct);
        var existing = await _db.CartItems.FirstOrDefaultAsync(
            ci => ci.CartId == cart.CartId && ci.ProductId == req.ProductId && ci.ProductVariantId == req.ProductVariantId, ct);

        var targetQty = (existing?.Quantity ?? 0) + req.Quantity;
        if (targetQty > available) throw new AppException($"Only {available} in stock.");
        var limit = await ItemLimitAsync(ct);
        if (limit > 0 && targetQty > limit) throw new AppException($"Limit is {limit} per item.");

        if (existing is null)
        {
            _db.CartItems.Add(new CartItemEntity
            {
                CartId = cart.CartId,
                ProductId = req.ProductId,
                ProductVariantId = req.ProductVariantId,
                Quantity = req.Quantity,
                UnitPrice = unitPrice,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Quantity = targetQty;
            existing.UnitPrice = unitPrice;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(cart, ct);
    }

    public async Task<CartDto> UpdateItemAsync(long? userId, string? sessionId, long itemId, int quantity, CancellationToken ct = default)
    {
        var cart = await FindCartAsync(userId, sessionId, ct);
        if (cart is null) throw new AppException("Cart not found.", 404);
        var item = await _db.CartItems.FirstOrDefaultAsync(ci => ci.CartItemId == itemId && ci.CartId == cart.CartId, ct)
            ?? throw new AppException("Cart item not found.", 404);

        if (quantity < 1)
        {
            _db.CartItems.Remove(item);
        }
        else
        {
            var available = await AvailableAsync(item.ProductId, ct);
            if (quantity > available) throw new AppException($"Only {available} in stock.");
            var limit = await ItemLimitAsync(ct);
            if (limit > 0 && quantity > limit) throw new AppException($"Limit is {limit} per item.");
            item.Quantity = quantity;
            item.UpdatedAt = DateTime.UtcNow;
        }
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(cart, ct);
    }

    public async Task<CartDto> RemoveItemAsync(long? userId, string? sessionId, long itemId, CancellationToken ct = default)
    {
        var cart = await FindCartAsync(userId, sessionId, ct);
        if (cart is null) throw new AppException("Cart not found.", 404);
        var item = await _db.CartItems.FirstOrDefaultAsync(ci => ci.CartItemId == itemId && ci.CartId == cart.CartId, ct);
        if (item is not null)
        {
            _db.CartItems.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return await BuildDtoAsync(cart, ct);
    }

    public async Task<CartDto> ClearAsync(long? userId, string? sessionId, CancellationToken ct = default)
    {
        var cart = await FindCartAsync(userId, sessionId, ct);
        if (cart is null) return await BuildDtoAsync(null, ct);
        var items = await _db.CartItems.Where(ci => ci.CartId == cart.CartId).ToListAsync(ct);
        _db.CartItems.RemoveRange(items);
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(cart, ct);
    }

    public async Task<CartDto> MergeAsync(long userId, string? sessionId, CancellationToken ct = default)
    {
        var userCart = await GetOrCreateCartAsync(userId, null, ct);
        if (!string.IsNullOrEmpty(sessionId))
        {
            var guest = await _db.Carts
                .FirstOrDefaultAsync(c => c.SessionId == sessionId && c.UserId == null && c.Status == "Active" && c.TenantId == Tenant, ct);
            if (guest is not null && guest.CartId != userCart.CartId)
            {
                var guestItems = await _db.CartItems.Where(ci => ci.CartId == guest.CartId).ToListAsync(ct);
                foreach (var gi in guestItems)
                {
                    var match = await _db.CartItems.FirstOrDefaultAsync(
                        ci => ci.CartId == userCart.CartId && ci.ProductId == gi.ProductId && ci.ProductVariantId == gi.ProductVariantId, ct);
                    var available = await AvailableAsync(gi.ProductId, ct);
                    if (match is null)
                    {
                        gi.CartId = userCart.CartId;
                        gi.Quantity = Math.Min(gi.Quantity, Math.Max(available, 1));
                        gi.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        match.Quantity = Math.Min(match.Quantity + gi.Quantity, Math.Max(available, match.Quantity));
                        match.UpdatedAt = DateTime.UtcNow;
                        _db.CartItems.Remove(gi);
                    }
                }
                guest.Status = "Converted";
                guest.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }
        return await BuildDtoAsync(userCart, ct);
    }

    public async Task<CartDto> SetNotesAsync(long? userId, string? sessionId, string? notes, CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(userId, sessionId, ct);
        cart.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 500)];
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(cart, ct);
    }

    // ----- helpers -----

    private Task<CartEntity?> FindCartAsync(long? userId, string? sessionId, CancellationToken ct)
    {
        if (userId is { } uid)
            return _db.Carts.FirstOrDefaultAsync(c => c.UserId == uid && c.Status == "Active" && c.TenantId == Tenant, ct);
        if (!string.IsNullOrEmpty(sessionId))
            return _db.Carts.FirstOrDefaultAsync(c => c.SessionId == sessionId && c.UserId == null && c.Status == "Active" && c.TenantId == Tenant, ct);
        return Task.FromResult<CartEntity?>(null);
    }

    private async Task<CartEntity> GetOrCreateCartAsync(long? userId, string? sessionId, CancellationToken ct)
    {
        var cart = await FindCartAsync(userId, sessionId, ct);
        if (cart is not null) return cart;
        if (userId is null && string.IsNullOrEmpty(sessionId))
            throw new AppException("No cart context. Provide a cart token.");

        cart = new CartEntity
        {
            TenantId = Tenant,
            UserId = userId,
            SessionId = userId is null ? sessionId : null,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync(ct);
        return cart;
    }

    private async Task<int> AvailableAsync(long productId, CancellationToken ct)
    {
        var isBundle = await _db.Products.Where(p => p.ProductId == productId).Select(p => p.IsBundle).FirstOrDefaultAsync(ct);
        if (isBundle) return await _bundles.AvailableQtyAsync(productId, ct);
        var sum = await _db.Inventory.Where(i => i.ProductId == productId).SumAsync(i => (int?)i.AvailableQty, ct);
        return sum ?? 0;
    }

    /// <summary>Merchant "max quantity per item" checkout setting (0 = no limit).</summary>
    private async Task<int> ItemLimitAsync(CancellationToken ct)
    {
        var v = await _db.Settings
            .Where(s => s.TenantId == Tenant && s.SettingKey == ecomm.api.Features.Settings.CheckoutSettingsService.ItemLimitKey)
            .Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return int.TryParse(v, out var n) && n > 0 ? n : 0;
    }

    private async Task<CartDto> BuildDtoAsync(CartEntity? cart, CancellationToken ct)
    {
        var taxMode = await _tax.GetTaxModeAsync(ct);
        if (cart is null) return new CartDto(0, Array.Empty<CartItemDto>(), 0, 0, 0m, taxMode);

        var rows = await _db.CartItems
            .Where(ci => ci.CartId == cart.CartId)
            .OrderBy(ci => ci.CartItemId)
            .Select(ci => new
            {
                ci.CartItemId, ci.ProductId, ci.ProductVariantId, ci.Quantity, ci.UnitPrice,
                ci.Product!.Name, ci.Product.Slug, ci.Product.IsBundle,
                ImageUrl = _db.ProductImages.Where(im => im.ProductId == ci.ProductId)
                    .OrderByDescending(im => im.IsPrimary).ThenBy(im => im.DisplayOrder)
                    .Select(im => im.Url).FirstOrDefault(),
                VariantLabel = ci.ProductVariantId == null ? null :
                    _db.ProductVariants.Where(v => v.ProductVariantId == ci.ProductVariantId).Select(v => v.Name).FirstOrDefault(),
                Available = _db.Inventory.Where(i => i.ProductId == ci.ProductId).Select(i => (int?)i.AvailableQty).Sum() ?? 0,
            })
            .ToListAsync(ct);

        var bundleAvailable = new Dictionary<long, int>();
        foreach (var r in rows.Where(r => r.IsBundle).Select(r => r.ProductId).Distinct())
            bundleAvailable[r] = await _bundles.AvailableQtyAsync(r, ct);

        var items = rows.Select(x =>
        {
            var available = x.IsBundle ? bundleAvailable[x.ProductId] : x.Available;
            return new CartItemDto(x.CartItemId, x.ProductId, x.ProductVariantId, x.Name, x.Slug, x.ImageUrl, x.VariantLabel,
                x.UnitPrice, x.Quantity, x.UnitPrice * x.Quantity, available, available > 0);
        }).ToList();

        return new CartDto(cart.CartId, items, items.Sum(i => i.Quantity), items.Count, items.Sum(i => i.LineTotal), taxMode, cart.Notes);
    }
}
