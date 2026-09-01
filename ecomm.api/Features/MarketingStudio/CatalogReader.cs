using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>A product as the Marketing Studio needs it — just enough to plan and headline a creative.</summary>
public sealed record CatalogProduct(long ProductId, string Name, decimal Price);

/// <summary>
/// The Marketing Studio's read-only window into the commerce catalog. A thin port so the module never
/// joins into core product tables directly — on extraction this becomes an HTTP call to the commerce
/// API, with no change to callers (marketing-studio-plan.md §3.10).
/// </summary>
public interface ICatalogReader
{
    /// <summary>Up to <paramref name="count"/> active products, newest first — candidates to feature.</summary>
    Task<IReadOnlyList<CatalogProduct>> TopProductsAsync(int count, CancellationToken ct = default);

    /// <summary>Resolve a product's display name (null if missing/deleted).</summary>
    Task<string?> ProductNameAsync(long productId, CancellationToken ct = default);
}

public sealed class CatalogReader(EcommerceDbContext db) : ICatalogReader
{
    public async Task<IReadOnlyList<CatalogProduct>> TopProductsAsync(int count, CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 50);
        // Product is ITenantScoped → auto-filtered to the current tenant.
        return await db.Products.AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .Select(p => new CatalogProduct(p.ProductId, p.Name, p.Price))
            .ToListAsync(ct);
    }

    public async Task<string?> ProductNameAsync(long productId, CancellationToken ct = default) =>
        await db.Products.AsNoTracking()
            .Where(p => p.ProductId == productId && !p.IsDeleted)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);
}
