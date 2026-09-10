using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

public interface ICustomTextFieldService
{
    Task<List<ProductCustomTextFieldDto>?> GetAsync(long productId, CancellationToken ct = default);
    Task<List<ProductCustomTextFieldDto>?> SetAsync(long productId, SetCustomTextFieldsRequest req, CancellationToken ct = default);
}

/// <summary>Admin-defined personalization fields on a product ("Custom text"). Saved as a whole set — same
/// replace-all-on-save shape as <see cref="ProductAttributeService"/> — since the admin UI edits the list as
/// one form with a single Save, not per-row CRUD.</summary>
public sealed class CustomTextFieldService : ICustomTextFieldService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;

    public CustomTextFieldService(EcommerceDbContext db) => _db = db;

    public async Task<List<ProductCustomTextFieldDto>?> GetAsync(long productId, CancellationToken ct = default)
    {
        if (!await ProductExists(productId, ct)) return null;
        return await Query(productId).ToListAsync(ct);
    }

    public async Task<List<ProductCustomTextFieldDto>?> SetAsync(long productId, SetCustomTextFieldsRequest req, CancellationToken ct = default)
    {
        if (!await ProductExists(productId, ct)) return null;

        var fields = req.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Label)).ToList();
        if (fields.Count > 10)
            throw new AppException("A product can have at most 10 custom text fields.");

        var existing = await _db.ProductCustomTextFields.Where(f => f.ProductId == productId).ToListAsync(ct);
        _db.ProductCustomTextFields.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var order = 0;
        foreach (var f in fields)
        {
            _db.ProductCustomTextFields.Add(new ProductCustomTextField
            {
                ProductId = productId,
                Label = f.Label.Trim(),
                MaxLength = Math.Clamp(f.MaxLength <= 0 ? 255 : f.MaxLength, 1, 2000),
                IsMandatory = f.IsMandatory,
                DisplayOrder = order++,
                CreatedAt = now,
            });
        }
        await _db.SaveChangesAsync(ct);
        return await Query(productId).ToListAsync(ct);
    }

    private IQueryable<ProductCustomTextFieldDto> Query(long productId) =>
        _db.ProductCustomTextFields.Where(f => f.ProductId == productId).OrderBy(f => f.DisplayOrder)
            .Select(f => new ProductCustomTextFieldDto(f.ProductCustomTextFieldId, f.Label, f.MaxLength, f.IsMandatory, f.DisplayOrder));

    private Task<bool> ProductExists(long productId, CancellationToken ct) =>
        _db.Products.AnyAsync(p => p.ProductId == productId && p.TenantId == Tenant && !p.IsDeleted, ct);
}
