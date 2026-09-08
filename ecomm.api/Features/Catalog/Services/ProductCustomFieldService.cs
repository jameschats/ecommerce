using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

public interface IProductCustomFieldService
{
    Task<List<ProductCustomFieldDto>?> GetAsync(long productId, CancellationToken ct = default);
    Task<List<ProductCustomFieldDto>?> SetAsync(long productId, SetProductCustomFieldsRequest req, CancellationToken ct = default);
}

/// <summary>
/// Admin-defined "custom text" fields per product — title, char limit, mandatory — answered by
/// the customer on the product page (design ask: Wix-style personalization field). Replace-all
/// semantics, same as <see cref="ProductAttributeService"/>: the admin form always sends the
/// complete list, so existing rows are dropped and rewritten rather than diffed.
/// </summary>
public sealed class ProductCustomFieldService : IProductCustomFieldService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;

    public ProductCustomFieldService(EcommerceDbContext db) => _db = db;

    public async Task<List<ProductCustomFieldDto>?> GetAsync(long productId, CancellationToken ct = default)
    {
        if (!await ProductExists(productId, ct)) return null;
        return await Query(productId).ToListAsync(ct);
    }

    public async Task<List<ProductCustomFieldDto>?> SetAsync(long productId, SetProductCustomFieldsRequest req, CancellationToken ct = default)
    {
        if (!await ProductExists(productId, ct)) return null;

        foreach (var f in req.Fields)
        {
            if (string.IsNullOrWhiteSpace(f.Label)) throw new AppException("Each custom field needs a title.");
            if (f.CharLimit < 1 || f.CharLimit > 2000) throw new AppException("Char limit must be between 1 and 2000.");
        }

        var existing = await _db.ProductCustomFields.Where(f => f.ProductId == productId).ToListAsync(ct);
        _db.ProductCustomFields.RemoveRange(existing);

        var now = DateTime.UtcNow;
        foreach (var f in req.Fields)
        {
            _db.ProductCustomFields.Add(new ProductCustomField
            {
                TenantId = Tenant,
                ProductId = productId,
                Label = f.Label.Trim(),
                CharLimit = f.CharLimit,
                IsMandatory = f.IsMandatory,
                SortOrder = f.SortOrder,
                CreatedAt = now,
            });
        }
        await _db.SaveChangesAsync(ct);
        return await Query(productId).ToListAsync(ct);
    }

    private IQueryable<ProductCustomFieldDto> Query(long productId) =>
        _db.ProductCustomFields.Where(f => f.ProductId == productId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.ProductCustomFieldId)
            .Select(f => new ProductCustomFieldDto(f.ProductCustomFieldId, f.Label, f.CharLimit, f.IsMandatory, f.SortOrder));

    private Task<bool> ProductExists(long productId, CancellationToken ct) =>
        _db.Products.AnyAsync(p => p.ProductId == productId && p.TenantId == Tenant && !p.IsDeleted, ct);
}
