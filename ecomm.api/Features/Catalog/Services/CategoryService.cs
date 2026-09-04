using ecomm.api.Common;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync(bool activeOnly, CancellationToken ct = default);
    Task<CategoryDto?> GetAsync(long id, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(SaveCategoryRequest req, CancellationToken ct = default);
    Task<CategoryDto?> UpdateAsync(long id, SaveCategoryRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class CategoryService : ICategoryService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;

    public CategoryService(EcommerceDbContext db) => _db = db;

    public Task<List<CategoryDto>> GetAllAsync(bool activeOnly, CancellationToken ct = default) =>
        _db.Categories
            .Where(c => c.TenantId == Tenant && (!activeOnly || c.IsActive))
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.CategoryId, c.ParentCategoryId, c.Name, c.Slug,
                c.Description, c.ImageUrl, c.DisplayOrder, c.IsActive))
            .ToListAsync(ct);

    public async Task<CategoryDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var c = await Find(id, ct);
        return c is null ? null : Map(c);
    }

    public async Task<CategoryDto> CreateAsync(SaveCategoryRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Name is required.");

        var category = new Category
        {
            TenantId = Tenant,
            Name = req.Name.Trim(),
            Slug = await UniqueSlugAsync(req.Slug ?? req.Name, null, ct),
            ParentCategoryId = req.ParentCategoryId,
            Description = req.Description,
            ImageUrl = req.ImageUrl,
            DisplayOrder = req.DisplayOrder,
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return Map(category);
    }

    public async Task<CategoryDto?> UpdateAsync(long id, SaveCategoryRequest req, CancellationToken ct = default)
    {
        var category = await Find(id, ct);
        if (category is null) return null;
        if (req.ParentCategoryId == id) throw new AppException("A category cannot be its own parent.");

        category.Name = req.Name.Trim();
        category.Slug = await UniqueSlugAsync(req.Slug ?? req.Name, id, ct);
        category.ParentCategoryId = req.ParentCategoryId;
        category.Description = req.Description;
        category.ImageUrl = req.ImageUrl;
        category.DisplayOrder = req.DisplayOrder;
        category.IsActive = req.IsActive;
        category.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(category);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var category = await Find(id, ct);
        if (category is null) return false;

        if (await _db.Categories.AnyAsync(c => c.ParentCategoryId == id, ct))
            throw new AppException("Cannot delete a category that has sub-categories.", StatusCodes.Status409Conflict);

        // Counted separately, because deleting a product hides it rather than removing the row:
        // Products.CategoryId is NOT NULL with an ON DELETE RESTRICT foreign key, so a deleted
        // product still holds its category. Checking only live products let the delete through
        // to the database, which refused it, and the admin saw "An unexpected error occurred"
        // with nothing on screen to explain why.
        var live = await _db.Products.CountAsync(p => p.CategoryId == id && !p.IsDeleted, ct);
        if (live > 0)
            throw new AppException(
                $"Cannot delete this category — {live} product{(live == 1 ? "" : "s")} still use{(live == 1 ? "s" : "")} it. "
                + "Move them to another category first.",
                StatusCodes.Status409Conflict);

        // Named rather than counted: these products are invisible everywhere in admin, so a bare
        // number would leave the admin looking for something they cannot see.
        var removed = await _db.Products
            .Where(p => p.CategoryId == id && p.IsDeleted)
            .OrderBy(p => p.ProductId)
            .Select(p => p.Name)
            .Take(4)
            .ToListAsync(ct);

        if (removed.Count > 0)
        {
            var total = await _db.Products.CountAsync(p => p.CategoryId == id && p.IsDeleted, ct);
            var names = string.Join(", ", removed.Take(3));
            if (total > 3) names += $" and {total - 3} more";

            throw new AppException(
                $"Cannot delete this category — it is still used by {total} deleted "
                + $"product{(total == 1 ? "" : "s")} ({names}). Those are kept so past orders and invoices "
                + "still say what was sold.",
                StatusCodes.Status409Conflict);
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private Task<Category?> Find(long id, CancellationToken ct) =>
        _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == id && c.TenantId == Tenant, ct);

    private async Task<string> UniqueSlugAsync(string source, long? excludeId, CancellationToken ct)
    {
        var baseSlug = Slug.From(source);
        var slug = baseSlug;
        var n = 1;
        while (await _db.Categories.AnyAsync(
            c => c.TenantId == Tenant && c.Slug == slug && c.CategoryId != (excludeId ?? 0), ct))
        {
            slug = $"{baseSlug}-{++n}";
        }
        return slug;
    }

    private static CategoryDto Map(Category c) =>
        new(c.CategoryId, c.ParentCategoryId, c.Name, c.Slug, c.Description, c.ImageUrl, c.DisplayOrder, c.IsActive);
}
