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
    private long Tenant => _db.CurrentTenantId;
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

        if (await _db.Products.AnyAsync(p => p.CategoryId == id && !p.IsDeleted, ct))
            throw new AppException("Cannot delete a category that still has products.", StatusCodes.Status409Conflict);
        if (await _db.Categories.AnyAsync(c => c.ParentCategoryId == id, ct))
            throw new AppException("Cannot delete a category that has sub-categories.", StatusCodes.Status409Conflict);

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
