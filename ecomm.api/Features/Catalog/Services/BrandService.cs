using ecomm.api.Common;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

public interface IBrandService
{
    Task<List<BrandDto>> GetAllAsync(bool activeOnly, CancellationToken ct = default);
    Task<BrandDto?> GetAsync(long id, CancellationToken ct = default);
    Task<BrandDto> CreateAsync(SaveBrandRequest req, CancellationToken ct = default);
    Task<BrandDto?> UpdateAsync(long id, SaveBrandRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class BrandService : IBrandService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;

    public BrandService(EcommerceDbContext db) => _db = db;

    public Task<List<BrandDto>> GetAllAsync(bool activeOnly, CancellationToken ct = default) =>
        _db.Brands
            .Where(b => b.TenantId == Tenant && (!activeOnly || b.IsActive))
            .OrderBy(b => b.Name)
            .Select(b => new BrandDto(b.BrandId, b.Name, b.Slug, b.LogoUrl, b.Description, b.IsActive))
            .ToListAsync(ct);

    public async Task<BrandDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var b = await Find(id, ct);
        return b is null ? null : Map(b);
    }

    public async Task<BrandDto> CreateAsync(SaveBrandRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Name is required.");

        var brand = new Brand
        {
            TenantId = Tenant,
            Name = req.Name.Trim(),
            Slug = await UniqueSlugAsync(req.Slug ?? req.Name, null, ct),
            LogoUrl = req.LogoUrl,
            Description = req.Description,
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Brands.Add(brand);
        await _db.SaveChangesAsync(ct);
        return Map(brand);
    }

    public async Task<BrandDto?> UpdateAsync(long id, SaveBrandRequest req, CancellationToken ct = default)
    {
        var brand = await Find(id, ct);
        if (brand is null) return null;

        brand.Name = req.Name.Trim();
        brand.Slug = await UniqueSlugAsync(req.Slug ?? req.Name, id, ct);
        brand.LogoUrl = req.LogoUrl;
        brand.Description = req.Description;
        brand.IsActive = req.IsActive;
        brand.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(brand);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var brand = await Find(id, ct);
        if (brand is null) return false;

        if (await _db.Products.AnyAsync(p => p.BrandId == id && !p.IsDeleted, ct))
            throw new AppException("Cannot delete a brand that still has products.", StatusCodes.Status409Conflict);

        _db.Brands.Remove(brand);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private Task<Brand?> Find(long id, CancellationToken ct) =>
        _db.Brands.FirstOrDefaultAsync(b => b.BrandId == id && b.TenantId == Tenant, ct);

    private async Task<string> UniqueSlugAsync(string source, long? excludeId, CancellationToken ct)
    {
        var baseSlug = Slug.From(source);
        var slug = baseSlug;
        var n = 1;
        while (await _db.Brands.AnyAsync(
            b => b.TenantId == Tenant && b.Slug == slug && b.BrandId != (excludeId ?? 0), ct))
        {
            slug = $"{baseSlug}-{++n}";
        }
        return slug;
    }

    private static BrandDto Map(Brand b) =>
        new(b.BrandId, b.Name, b.Slug, b.LogoUrl, b.Description, b.IsActive);
}
