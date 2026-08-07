using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Checkout;

public sealed record SaveEstimateRequest(IReadOnlyList<QuickOrderLineRequest> Lines);

public sealed record AbandonedEstimateDto(
    long CartId, long? UserId, string? CustomerName, string? Email, string? Phone,
    int ItemCount, decimal Value, DateTime LastActivity, DateTime? RemindedAt);

public interface IAbandonedEstimateService
{
    Task SaveAsync(long userId, SaveEstimateRequest req, CancellationToken ct = default);
    Task MarkConvertedAsync(long userId, CancellationToken ct = default);
    Task<List<AbandonedEstimateDto>> ListAsync(int idleHours, CancellationToken ct = default);
    Task<bool> RemindAsync(long cartId, CancellationToken ct = default);
}

/// <summary>
/// Baskets left unfinished.
///
/// Quick-order keeps quantities in the browser, so the Cart table — which has documented an
/// "Abandoned" status since migration 005 — was never written to and there was nothing to
/// report. The estimate is now mirrored here for signed-in shoppers, which is also the only
/// group worth tracking: chasing an abandoned basket means emailing someone, and an
/// anonymous visitor has left no address to email.
/// </summary>
public sealed class AbandonedEstimateService : IAbandonedEstimateService
{
    private const long Tenant = 1;

    private readonly EcommerceDbContext _db;
    private readonly Notifications.IEmailSender _email;

    private readonly Notifications.INotificationPolicy _policy;

    public AbandonedEstimateService(
        EcommerceDbContext db, Notifications.IEmailSender email, Notifications.INotificationPolicy policy)
    {
        _db = db;
        _email = email;
        _policy = policy;
    }

    public async Task SaveAsync(long userId, SaveEstimateRequest req, CancellationToken ct = default)
    {
        var cart = await _db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.TenantId == Tenant && c.UserId == userId && c.Status == "Active", ct);

        var lines = (req.Lines ?? []).Where(l => l.Quantity > 0).ToList();

        // An emptied basket is not an abandoned one. Closing it out stops a customer who
        // deliberately cleared their estimate being chased about it.
        if (lines.Count == 0)
        {
            if (cart is not null)
            {
                _db.CartItems.RemoveRange(cart.Items);
                _db.Carts.Remove(cart);
                await _db.SaveChangesAsync(ct);
            }
            return;
        }

        var now = DateTime.UtcNow;
        if (cart is null)
        {
            // Fully qualified: the Features.Cart namespace shadows the entity name here.
            cart = new Data.Entities.Cart { TenantId = Tenant, UserId = userId, Status = "Active", CreatedAt = now };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            _db.CartItems.RemoveRange(cart.Items);
        }

        // Prices are snapshotted so the admin list shows what the basket was worth to the
        // buyer at the time, not what it would cost after a price change.
        var ids = lines.Select(l => l.ProductId).ToList();
        var prices = await _db.Products
            .Where(p => p.TenantId == Tenant && ids.Contains(p.ProductId))
            .Select(p => new { p.ProductId, p.Price })
            .ToDictionaryAsync(x => x.ProductId, x => x.Price, ct);

        foreach (var l in lines)
        {
            if (!prices.TryGetValue(l.ProductId, out var price)) continue;
            _db.CartItems.Add(new CartItem
            {
                CartId = cart.CartId,
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = price,
                CreatedAt = now,
            });
        }

        cart.Status = "Active";
        // Cleared so a basket the customer has come back to and changed can be chased again
        // if they abandon it a second time.
        cart.AbandonedRemindedAt = null;
        cart.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Called when an order is placed — the basket became a sale, not a loss.</summary>
    public async Task MarkConvertedAsync(long userId, CancellationToken ct = default)
    {
        var carts = await _db.Carts
            .Where(c => c.TenantId == Tenant && c.UserId == userId && c.Status == "Active")
            .ToListAsync(ct);

        foreach (var c in carts)
        {
            c.Status = "Converted";
            c.UpdatedAt = DateTime.UtcNow;
        }
        if (carts.Count > 0) await _db.SaveChangesAsync(ct);
    }

    public async Task<List<AbandonedEstimateDto>> ListAsync(int idleHours, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-Math.Max(1, idleHours));

        // Derived on read rather than flipped by a scheduled job. There is no scheduler in
        // this app, and "idle since" is a question the query can answer directly — a status
        // column set by a job would only be a cache of this, able to go stale.
        return await _db.Carts
            .Where(c => c.TenantId == Tenant && c.Status == "Active"
                        && c.UserId != null && c.Items.Any()
                        && (c.UpdatedAt ?? c.CreatedAt) < cutoff)
            .OrderBy(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => new AbandonedEstimateDto(
                c.CartId,
                c.UserId,
                _db.Users.Where(u => u.UserId == c.UserId).Select(u => u.FullName).FirstOrDefault(),
                _db.Users.Where(u => u.UserId == c.UserId).Select(u => u.Email).FirstOrDefault(),
                _db.Users.Where(u => u.UserId == c.UserId).Select(u => u.PhoneNumber).FirstOrDefault(),
                c.Items.Sum(i => i.Quantity),
                c.Items.Sum(i => i.UnitPrice * i.Quantity),
                c.UpdatedAt ?? c.CreatedAt,
                c.AbandonedRemindedAt))
            .ToListAsync(ct);
    }

    public async Task<bool> RemindAsync(long cartId, CancellationToken ct = default)
    {
        var cart = await _db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CartId == cartId && c.TenantId == Tenant, ct);
        if (cart is null) return false;

        var email = await _db.Users.Where(u => u.UserId == cart.UserId)
            .Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(email))
            throw new AppException("That customer has no email address on file.", StatusCodes.Status400BadRequest);

        if (cart.AbandonedRemindedAt is not null)
            throw new AppException("A reminder has already been sent for this basket.");

        var name = await _db.Users.Where(u => u.UserId == cart.UserId)
            .Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var storeName = await _db.Settings.Where(s => s.SettingKey == "Site.Name")
            .Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        var store = string.IsNullOrWhiteSpace(storeName) ? "CalendarShop" : storeName;

        var items = cart.Items.Sum(i => i.Quantity);
        var value = cart.Items.Sum(i => i.UnitPrice * i.Quantity);

        var body = $"""
            <p>Hi {(string.IsNullOrWhiteSpace(name) ? "there" : name)},</p>
            <p>You left {items} item(s) in your estimate, worth about ₹{value:N0}.</p>
            <p>Your quantities are still saved — open the price list and pick up where you left off.</p>
            <p>— {store}</p>
            """;

        if (!await _policy.IsEnabledAsync("AbandonedEstimate", "Email", ct)) return false;

        await _email.SendAsync(email!, $"Your estimate is still waiting — {store}", body, ct);

        cart.AbandonedRemindedAt = DateTime.UtcNow;
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
