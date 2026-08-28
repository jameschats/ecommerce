using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Inventory;

public interface IBackInStockService
{
    /// <summary>Register a shopper to be emailed when this product is back in stock.</summary>
    Task RequestAsync(long productId, string email, long? userId, CancellationToken ct = default);

    /// <summary>Fired when a product transitions 0 → in stock: email everyone waiting, then mark them notified.
    /// Best-effort — never throws into the inventory update that triggered it.</summary>
    Task NotifyRestockAsync(long productId, CancellationToken ct = default);
}

public sealed class BackInStockService(
    EcommerceDbContext db, IEmailSender email, IOptions<TenancyOptions> tenancy, ILogger<BackInStockService> log) : IBackInStockService
{
    public async Task RequestAsync(long productId, string email, long? userId, CancellationToken ct = default)
    {
        var addr = (email ?? "").Trim();
        if (addr.Length < 3 || !addr.Contains('@')) throw new AppException("Enter a valid email address.", StatusCodes.Status400BadRequest);

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId && p.IsActive && !p.IsDeleted, ct);
        if (product is null) throw new AppException("Product not found.", StatusCodes.Status404NotFound);

        // One live (un-notified) request per email+product — clicking twice is idempotent.
        var already = await db.BackInStockRequests.AnyAsync(
            r => r.ProductId == productId && r.Email == addr && r.NotifiedAt == null, ct);
        if (already) return;

        db.BackInStockRequests.Add(new BackInStockRequest
        {
            ProductId = productId, Email = addr, UserId = userId, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task NotifyRestockAsync(long productId, CancellationToken ct = default)
    {
        try
        {
            var product = await db.Products.AsNoTracking()
                .Where(p => p.ProductId == productId)
                .Select(p => new { p.Name, p.Slug })
                .FirstOrDefaultAsync(ct);
            if (product is null) return;

            var pending = await db.BackInStockRequests
                .Where(r => r.ProductId == productId && r.NotifiedAt == null)
                .ToListAsync(ct);

            // Wishlist owners get a courtesy restock alert too, throttled so a product that flaps
            // in and out of stock doesn't spam them.
            var wlCutoff = DateTime.UtcNow.AddDays(-14);
            var wishlist = await db.WishlistItems
                .Where(w => w.ProductId == productId && (w.RestockNotifiedAt == null || w.RestockNotifiedAt < wlCutoff))
                .ToListAsync(ct);

            if (pending.Count == 0 && wishlist.Count == 0) return;

            var url = await BuildProductUrlAsync(product.Slug, ct);
            var subject = $"Back in stock: {product.Name}";
            var body = $"<p>Good news — <strong>{System.Net.WebUtility.HtmlEncode(product.Name)}</strong> is back in stock.</p>"
                     + (url is null ? "" : $"<p><a href=\"{url}\">View it now</a> before it sells out again.</p>");

            var now = DateTime.UtcNow;
            var sentTo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) Explicit "notify me" opt-ins (consumed — NotifiedAt set).
            foreach (var r in pending)
                if (await TrySendAsync(r.Email, subject, body, productId, ct)) { r.NotifiedAt = now; sentTo.Add(r.Email); }

            // 2) Wishlist owners, skipping anyone already emailed above.
            if (wishlist.Count > 0)
            {
                var userIds = wishlist.Select(w => w.UserId).Distinct().ToList();
                var emailByUser = (await db.Users
                        .Where(u => userIds.Contains(u.UserId) && u.Email != null && !u.IsDeleted)
                        .Select(u => new { u.UserId, u.Email }).ToListAsync(ct))
                    .ToDictionary(x => x.UserId, x => x.Email!);
                foreach (var w in wishlist)
                {
                    if (!emailByUser.TryGetValue(w.UserId, out var addr)) continue;
                    if (sentTo.Contains(addr)) { w.RestockNotifiedAt = now; continue; }   // already got the opt-in email
                    if (await TrySendAsync(addr, subject, body, productId, ct)) { w.RestockNotifiedAt = now; sentTo.Add(addr); }
                }
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "NotifyRestockAsync failed for product {ProductId} (non-fatal).", productId);
        }
    }

    private async Task<bool> TrySendAsync(string toEmail, string subject, string body, long productId, CancellationToken ct)
    {
        try { await email.SendAsync(toEmail, subject, body, ct); return true; }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Restock email to {Email} for product {ProductId} failed.", toEmail, productId);
            return false;
        }
    }

    /// <summary>Best-effort storefront URL for the current tenant: https://{slug}.{baseDomain}/product/{slug}.</summary>
    private async Task<string?> BuildProductUrlAsync(string productSlug, CancellationToken ct)
    {
        var baseDomain = (tenancy.Value.BaseDomain ?? "").Trim();
        if (string.IsNullOrEmpty(baseDomain)) return null;
        var storeSlug = await db.Tenants.AsNoTracking()
            .Where(t => t.TenantId == db.CurrentTenantId)
            .Select(t => t.CustomDomainVerified && t.CustomDomain != null ? t.CustomDomain : $"{t.Slug}.{baseDomain}")
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrEmpty(storeSlug) ? null : $"https://{storeSlug}/product/{productSlug}";
    }
}
