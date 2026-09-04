using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

/// <summary>
/// What deleting a category would destroy. Every number here is something the confirmation
/// dialog must say out loud before anyone clicks through it.
/// </summary>
public sealed record CategoryDeleteImpact(
    long CategoryId, string Name,
    int LiveProducts, int DeletedProducts,
    int Orders, int Invoices, int Payments, decimal OrderValue,
    /// <summary>
    /// Products in *other* categories that sit on the same orders. Deleting the orders takes
    /// their history too, which is the part nobody expects.
    /// </summary>
    int OtherCategoryProductsAffected,
    bool CanDeleteOutright);

public interface ICategoryDeletionService
{
    Task<CategoryDeleteImpact> ImpactAsync(long categoryId, CancellationToken ct = default);
    Task<CategoryDeleteImpact> CascadeDeleteAsync(long categoryId, string confirmName, CancellationToken ct = default);
}

/// <summary>
/// Deleting a category and, on request, everything holding it down.
///
/// Kept apart from CategoryService because the ordinary delete is a safe, guarded operation
/// and this is not: it removes orders, and an order takes its invoice, its payments, its
/// shipments and its status history with it through the database's own cascades. That is a
/// legitimate thing to want while a shop is still full of test data, and a serious thing to
/// do afterwards, so it is deliberately a separate call that has to be asked for by name.
/// </summary>
public sealed class CategoryDeletionService : ICategoryDeletionService
{
    private const long Tenant = 1;

    private readonly EcommerceDbContext _db;
    private readonly ILogger<CategoryDeletionService> _log;

    public CategoryDeletionService(EcommerceDbContext db, ILogger<CategoryDeletionService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<CategoryDeleteImpact> ImpactAsync(long categoryId, CancellationToken ct = default)
    {
        var category = await _db.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId && c.TenantId == Tenant, ct)
            ?? throw new AppException("Category not found.", StatusCodes.Status404NotFound);

        var productIds = await _db.Products.AsNoTracking()
            .Where(p => p.CategoryId == categoryId)
            .Select(p => p.ProductId)
            .ToListAsync(ct);

        var live = await _db.Products.CountAsync(p => p.CategoryId == categoryId && !p.IsDeleted, ct);
        var removed = productIds.Count - live;

        var orderIds = productIds.Count == 0
            ? new List<long>()
            : await _db.OrderItems.AsNoTracking()
                .Where(oi => productIds.Contains(oi.ProductId))
                .Select(oi => oi.OrderId)
                .Distinct()
                .ToListAsync(ct);

        var invoices = orderIds.Count == 0 ? 0
            : await _db.Invoices.CountAsync(i => orderIds.Contains(i.OrderId), ct);
        var payments = orderIds.Count == 0 ? 0
            : await _db.Payments.CountAsync(p => orderIds.Contains(p.OrderId), ct);
        var value = orderIds.Count == 0 ? 0m
            : await _db.Orders.Where(o => orderIds.Contains(o.OrderId))
                .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

        // Other products riding on the same orders. Their sales history disappears too, and a
        // dialog that only counted this category's products would not have mentioned it.
        var others = orderIds.Count == 0 ? 0
            : await _db.OrderItems.AsNoTracking()
                .Where(oi => orderIds.Contains(oi.OrderId) && !productIds.Contains(oi.ProductId))
                .Select(oi => oi.ProductId)
                .Distinct()
                .CountAsync(ct);

        var hasChildren = await _db.Categories.AnyAsync(c => c.ParentCategoryId == categoryId, ct);

        return new CategoryDeleteImpact(
            categoryId, category.Name, live, removed,
            orderIds.Count, invoices, payments, value, others,
            CanDeleteOutright: productIds.Count == 0 && !hasChildren);
    }

    public async Task<CategoryDeleteImpact> CascadeDeleteAsync(
        long categoryId, string confirmName, CancellationToken ct = default)
    {
        var impact = await ImpactAsync(categoryId, ct);

        // Typed, not clicked. This removes trading records, and a Yes button sitting next to a
        // Delete link is one slip away from taking invoices with it.
        if (!string.Equals(confirmName?.Trim(), impact.Name, StringComparison.OrdinalIgnoreCase))
            throw new AppException(
                $"Type the category name exactly — \"{impact.Name}\" — to confirm this deletion.");

        if (await _db.Categories.AnyAsync(c => c.ParentCategoryId == categoryId, ct))
            throw new AppException(
                "This category has sub-categories. Delete or move those first — a cascade that "
                + "silently took whole branches of the catalogue would be too easy to regret.",
                StatusCodes.Status409Conflict);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var productIds = await _db.Products
            .Where(p => p.CategoryId == categoryId)
            .Select(p => p.ProductId)
            .ToListAsync(ct);

        if (productIds.Count > 0)
        {
            // Orders first. The database cascades each one to its items, invoice, payments,
            // shipments, status history and coupon usage, so this single delete is the whole
            // of it — which is exactly why the caller had to type the name.
            var orderIds = await _db.OrderItems
                .Where(oi => productIds.Contains(oi.ProductId))
                .Select(oi => oi.OrderId)
                .Distinct()
                .ToListAsync(ct);

            if (orderIds.Count > 0)
            {
                var orders = await _db.Orders.Where(o => orderIds.Contains(o.OrderId)).ToListAsync(ct);
                _db.Orders.RemoveRange(orders);
                await _db.SaveChangesAsync(ct);
            }

            var products = await _db.Products.Where(p => productIds.Contains(p.ProductId)).ToListAsync(ct);
            _db.Products.RemoveRange(products);
            await _db.SaveChangesAsync(ct);
        }

        var category = await _db.Categories.FirstAsync(c => c.CategoryId == categoryId, ct);
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        // Logged because nothing else records it: the rows that would have shown what happened
        // are the rows that were deleted.
        _log.LogWarning(
            "Category {Id} \"{Name}\" deleted with cascade: {Products} products, {Orders} orders, "
            + "{Invoices} invoices, {Payments} payments, order value {Value}.",
            categoryId, impact.Name, impact.LiveProducts + impact.DeletedProducts,
            impact.Orders, impact.Invoices, impact.Payments, impact.OrderValue);

        return impact;
    }
}
