using ecomm.api.Common;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

public interface IAttributeService
{
    Task<List<AttributeDto>> ListAsync(CancellationToken ct = default);
    Task<AttributeDto?> GetAsync(long id, CancellationToken ct = default);
    Task<AttributeDto> CreateAsync(SaveAttributeRequest req, CancellationToken ct = default);
    Task<AttributeDto?> UpdateAsync(long id, SaveAttributeRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    Task<AttributeValueDto?> AddValueAsync(long attributeId, SaveAttributeValueRequest req, CancellationToken ct = default);
    Task<bool> DeleteValueAsync(long attributeId, long valueId, CancellationToken ct = default);
}

public sealed class AttributeService : IAttributeService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;

    public AttributeService(EcommerceDbContext db) => _db = db;

    public Task<List<AttributeDto>> ListAsync(CancellationToken ct = default) =>
        _db.Attributes.Where(a => a.TenantId == Tenant).OrderBy(a => a.Name)
            .Select(a => new AttributeDto(a.AttributeId, a.Name, a.Code, a.DataType, a.IsFilterable, a.IsActive,
                a.Values.OrderBy(v => v.Value).Select(v => new AttributeValueDto(v.AttributeValueId, v.Value)).ToList()))
            .ToListAsync(ct);

    public async Task<AttributeDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var dto = await _db.Attributes.Where(a => a.AttributeId == id && a.TenantId == Tenant)
            .Select(a => new AttributeDto(a.AttributeId, a.Name, a.Code, a.DataType, a.IsFilterable, a.IsActive,
                a.Values.OrderBy(v => v.Value).Select(v => new AttributeValueDto(v.AttributeValueId, v.Value)).ToList()))
            .FirstOrDefaultAsync(ct);
        return dto;
    }

    public async Task<AttributeDto> CreateAsync(SaveAttributeRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Name is required.");
        var code = Slug.From(req.Code ?? req.Name);
        if (await _db.Attributes.AnyAsync(a => a.TenantId == Tenant && a.Code == code, ct))
            throw new AppException($"Attribute code '{code}' already exists.", StatusCodes.Status409Conflict);

        var attribute = new AttributeDefinition
        {
            TenantId = Tenant,
            Name = req.Name.Trim(),
            Code = code,
            DataType = req.DataType,
            IsFilterable = req.IsFilterable,
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Attributes.Add(attribute);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(attribute.AttributeId, ct))!;
    }

    public async Task<AttributeDto?> UpdateAsync(long id, SaveAttributeRequest req, CancellationToken ct = default)
    {
        var attribute = await Find(id, ct);
        if (attribute is null) return null;

        attribute.Name = req.Name.Trim();
        attribute.DataType = req.DataType;
        attribute.IsFilterable = req.IsFilterable;
        attribute.IsActive = req.IsActive;
        attribute.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var attribute = await Find(id, ct);
        if (attribute is null) return false;
        if (await _db.ProductAttributeValues.AnyAsync(p => p.AttributeId == id, ct))
            throw new AppException("Cannot delete an attribute that is assigned to products.", StatusCodes.Status409Conflict);

        _db.Attributes.Remove(attribute);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AttributeValueDto?> AddValueAsync(long attributeId, SaveAttributeValueRequest req, CancellationToken ct = default)
    {
        var attribute = await Find(attributeId, ct);
        if (attribute is null) return null;
        if (string.IsNullOrWhiteSpace(req.Value)) throw new AppException("Value is required.");

        var value = new AttributeValue { AttributeId = attributeId, Value = req.Value.Trim(), CreatedAt = DateTime.UtcNow };
        _db.AttributeValues.Add(value);
        await _db.SaveChangesAsync(ct);
        return new AttributeValueDto(value.AttributeValueId, value.Value);
    }

    public async Task<bool> DeleteValueAsync(long attributeId, long valueId, CancellationToken ct = default)
    {
        var value = await _db.AttributeValues.FirstOrDefaultAsync(v => v.AttributeValueId == valueId && v.AttributeId == attributeId, ct);
        if (value is null) return false;
        _db.AttributeValues.Remove(value);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private Task<AttributeDefinition?> Find(long id, CancellationToken ct) =>
        _db.Attributes.FirstOrDefaultAsync(a => a.AttributeId == id && a.TenantId == Tenant, ct);
}
