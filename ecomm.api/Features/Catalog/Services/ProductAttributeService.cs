using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

public interface IProductAttributeService
{
    Task<List<ProductAttributeValueDto>?> GetAsync(long productId, CancellationToken ct = default);
    Task<List<ProductAttributeValueDto>?> SetAsync(long productId, SetProductAttributesRequest req, CancellationToken ct = default);
}

public sealed class ProductAttributeService : IProductAttributeService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;

    public ProductAttributeService(EcommerceDbContext db) => _db = db;

    public async Task<List<ProductAttributeValueDto>?> GetAsync(long productId, CancellationToken ct = default)
    {
        if (!await ProductExists(productId, ct)) return null;
        return await Query(productId).ToListAsync(ct);
    }

    public async Task<List<ProductAttributeValueDto>?> SetAsync(long productId, SetProductAttributesRequest req, CancellationToken ct = default)
    {
        if (!await ProductExists(productId, ct)) return null;

        var attributeIds = req.Attributes.Select(a => a.AttributeId).Distinct().ToList();
        var validIds = await _db.Attributes
            .Where(a => a.TenantId == Tenant && attributeIds.Contains(a.AttributeId))
            .Select(a => a.AttributeId).ToListAsync(ct);
        var missing = attributeIds.Except(validIds).ToList();
        if (missing.Count > 0)
            throw new AppException($"Unknown attribute id(s): {string.Join(", ", missing)}.");

        var existing = await _db.ProductAttributeValues.Where(p => p.ProductId == productId).ToListAsync(ct);
        _db.ProductAttributeValues.RemoveRange(existing);

        var now = DateTime.UtcNow;
        foreach (var a in req.Attributes)
        {
            _db.ProductAttributeValues.Add(new ProductAttributeValue
            {
                ProductId = productId,
                AttributeId = a.AttributeId,
                AttributeValueId = a.AttributeValueId,
                ValueText = a.ValueText,
                CreatedAt = now,
            });
        }
        await _db.SaveChangesAsync(ct);
        return await Query(productId).ToListAsync(ct);
    }

    private IQueryable<ProductAttributeValueDto> Query(long productId) =>
        _db.ProductAttributeValues.Where(p => p.ProductId == productId)
            .Select(a => new ProductAttributeValueDto(a.ProductAttributeValueId, a.AttributeId, a.Attribute!.Name,
                a.AttributeValueId, a.Value != null ? a.Value.Value : null, a.ValueText));

    private Task<bool> ProductExists(long productId, CancellationToken ct) =>
        _db.Products.AnyAsync(p => p.ProductId == productId && p.TenantId == Tenant && !p.IsDeleted, ct);
}
