using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Suppliers;

public sealed record SupplierDto(long SupplierId, string Name, string? Code, string? ContactName, string? Email,
    string? Phone, string? Gstin, string? City, string? State, int? LeadTimeDays, bool IsActive);
public sealed record SaveSupplierRequest(string Name, string? Code, string? ContactName, string? Email,
    string? Phone, string? Gstin, string? AddressLine1, string? City, string? State, string? Pincode,
    string? PaymentTerms, int? LeadTimeDays, string? Notes, bool IsActive);

public sealed record ProductSupplierDto(long ProductSupplierId, long SupplierId, string SupplierName,
    string? SupplierSku, decimal? CostPrice, int? LeadTimeDays, bool IsPrimary);
public sealed record ProductSupplierInput(long SupplierId, string? SupplierSku, decimal? CostPrice, int? LeadTimeDays, bool IsPrimary);

public interface ISupplierService
{
    Task<List<SupplierDto>> ListAsync(CancellationToken ct = default);
    Task<SupplierDto> CreateAsync(SaveSupplierRequest req, CancellationToken ct = default);
    Task<SupplierDto> UpdateAsync(long id, SaveSupplierRequest req, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task<List<ProductSupplierDto>> GetForProductAsync(long productId, CancellationToken ct = default);
    Task<List<ProductSupplierDto>> SetForProductAsync(long productId, List<ProductSupplierInput> items, CancellationToken ct = default);
}

public sealed class SupplierService : ISupplierService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;
    public SupplierService(EcommerceDbContext db) => _db = db;

    public Task<List<SupplierDto>> ListAsync(CancellationToken ct = default) =>
        _db.Suppliers.AsNoTracking().Where(s => s.TenantId == Tenant).OrderBy(s => s.Name)
            .Select(s => new SupplierDto(s.SupplierId, s.Name, s.Code, s.ContactName, s.Email, s.Phone, s.Gstin, s.City, s.State, s.LeadTimeDays, s.IsActive))
            .ToListAsync(ct);

    public async Task<SupplierDto> CreateAsync(SaveSupplierRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Supplier name is required.");
        var s = new Supplier { TenantId = Tenant, CreatedAt = DateTime.UtcNow };
        Apply(s, req);
        _db.Suppliers.Add(s);
        await _db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task<SupplierDto> UpdateAsync(long id, SaveSupplierRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Supplier name is required.");
        var s = await Find(id, ct);
        Apply(s, req);
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var s = await Find(id, ct);
        var links = await _db.ProductSuppliers.Where(ps => ps.SupplierId == id).ToListAsync(ct);
        _db.ProductSuppliers.RemoveRange(links);
        _db.Suppliers.Remove(s);
        await _db.SaveChangesAsync(ct);
    }

    public Task<List<ProductSupplierDto>> GetForProductAsync(long productId, CancellationToken ct = default) =>
        (from ps in _db.ProductSuppliers.AsNoTracking()
         join s in _db.Suppliers on ps.SupplierId equals s.SupplierId
         where ps.TenantId == Tenant && ps.ProductId == productId
         orderby ps.IsPrimary descending, s.Name
         select new ProductSupplierDto(ps.ProductSupplierId, ps.SupplierId, s.Name, ps.SupplierSku, ps.CostPrice, ps.LeadTimeDays, ps.IsPrimary))
        .ToListAsync(ct);

    public async Task<List<ProductSupplierDto>> SetForProductAsync(long productId, List<ProductSupplierInput> items, CancellationToken ct = default)
    {
        if (!await _db.Products.AnyAsync(p => p.ProductId == productId && p.TenantId == Tenant, ct))
            throw new AppException("Product not found.", 404);

        var existing = await _db.ProductSuppliers.Where(ps => ps.TenantId == Tenant && ps.ProductId == productId).ToListAsync(ct);
        _db.ProductSuppliers.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var primaryTaken = false;
        foreach (var i in items.Where(x => x.SupplierId > 0))
        {
            var isPrimary = i.IsPrimary && !primaryTaken;
            if (isPrimary) primaryTaken = true;
            _db.ProductSuppliers.Add(new ProductSupplier
            {
                TenantId = Tenant, ProductId = productId, SupplierId = i.SupplierId,
                SupplierSku = i.SupplierSku, CostPrice = i.CostPrice, LeadTimeDays = i.LeadTimeDays,
                IsPrimary = isPrimary, IsActive = true, CreatedAt = now,
            });
        }
        await _db.SaveChangesAsync(ct);
        return await GetForProductAsync(productId, ct);
    }

    private async Task<Supplier> Find(long id, CancellationToken ct) =>
        await _db.Suppliers.FirstOrDefaultAsync(s => s.TenantId == Tenant && s.SupplierId == id, ct)
        ?? throw new AppException("Supplier not found.", 404);

    private static void Apply(Supplier s, SaveSupplierRequest r)
    {
        s.Name = r.Name.Trim(); s.Code = r.Code?.Trim(); s.ContactName = r.ContactName?.Trim();
        s.Email = r.Email?.Trim(); s.Phone = r.Phone?.Trim(); s.Gstin = r.Gstin?.Trim();
        s.AddressLine1 = r.AddressLine1?.Trim(); s.City = r.City?.Trim(); s.State = r.State?.Trim();
        s.Pincode = r.Pincode?.Trim(); s.PaymentTerms = r.PaymentTerms?.Trim();
        s.LeadTimeDays = r.LeadTimeDays; s.Notes = r.Notes?.Trim(); s.IsActive = r.IsActive;
    }

    private static SupplierDto ToDto(Supplier s) =>
        new(s.SupplierId, s.Name, s.Code, s.ContactName, s.Email, s.Phone, s.Gstin, s.City, s.State, s.LeadTimeDays, s.IsActive);
}
