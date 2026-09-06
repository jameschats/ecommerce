using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Cms;

/// <summary>What the storefront renders per testimonial (photo already resolved to a URL).</summary>
public sealed record TestimonialDto(long TestimonialId, string Name, string? RoleOrCompany, string Quote, byte Rating, string? PhotoUrl);

/// <summary>Full testimonial row for the admin editor.</summary>
public sealed record AdminTestimonialDto(
    long TestimonialId, string Name, string? RoleOrCompany, string Quote, byte Rating,
    string? PhotoUrl, bool HasUpload, int DisplayOrder, bool IsActive);

public sealed record TestimonialUpsert(
    string Name, string? RoleOrCompany, string Quote, byte Rating, string? PhotoUrl, int DisplayOrder, bool IsActive);

public interface ITestimonialService
{
    Task<List<TestimonialDto>> GetActiveAsync(CancellationToken ct = default);
    Task<List<AdminTestimonialDto>> GetAllAsync(CancellationToken ct = default);
    Task<AdminTestimonialDto> CreateAsync(TestimonialUpsert req, CancellationToken ct = default);
    Task<AdminTestimonialDto> UpdateAsync(long id, TestimonialUpsert req, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task SetPhotoAsync(long id, byte[] data, string contentType, CancellationToken ct = default);
    Task<(byte[] Data, string ContentType)?> GetPhotoAsync(long id, CancellationToken ct = default);
}

public sealed class TestimonialService : ITestimonialService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;

    public TestimonialService(EcommerceDbContext db) => _db = db;

    public async Task<List<TestimonialDto>> GetActiveAsync(CancellationToken ct = default)
    {
        var rows = await _db.Testimonials.AsNoTracking()
            .Where(t => t.TenantId == Tenant && t.IsActive)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.TestimonialId)
            .Select(t => new { t.TestimonialId, t.Name, t.RoleOrCompany, t.Quote, t.Rating, t.PhotoUrl, HasUpload = t.PhotoData != null, t.UpdatedAt, t.CreatedAt })
            .ToListAsync(ct);

        return rows.Select(t => new TestimonialDto(
            t.TestimonialId, t.Name, t.RoleOrCompany, t.Quote, t.Rating,
            ResolvePhoto(t.TestimonialId, t.HasUpload, t.PhotoUrl, t.UpdatedAt ?? t.CreatedAt))).ToList();
    }

    public async Task<List<AdminTestimonialDto>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _db.Testimonials.AsNoTracking()
            .Where(t => t.TenantId == Tenant)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.TestimonialId)
            .Select(t => new { t.TestimonialId, t.Name, t.RoleOrCompany, t.Quote, t.Rating, t.PhotoUrl, HasUpload = t.PhotoData != null, t.DisplayOrder, t.IsActive, t.UpdatedAt, t.CreatedAt })
            .ToListAsync(ct);

        return rows.Select(t => new AdminTestimonialDto(
            t.TestimonialId, t.Name, t.RoleOrCompany, t.Quote, t.Rating,
            ResolvePhoto(t.TestimonialId, t.HasUpload, t.PhotoUrl, t.UpdatedAt ?? t.CreatedAt),
            t.HasUpload, t.DisplayOrder, t.IsActive)).ToList();
    }

    public async Task<AdminTestimonialDto> CreateAsync(TestimonialUpsert req, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var t = new Testimonial
        {
            TenantId = Tenant, Name = req.Name.Trim(), RoleOrCompany = req.RoleOrCompany?.Trim(),
            Quote = req.Quote.Trim(), Rating = Clamp(req.Rating),
            PhotoUrl = string.IsNullOrWhiteSpace(req.PhotoUrl) ? null : req.PhotoUrl.Trim(),
            DisplayOrder = req.DisplayOrder, IsActive = req.IsActive, CreatedAt = now,
        };
        _db.Testimonials.Add(t);
        await _db.SaveChangesAsync(ct);
        return ToAdmin(t);
    }

    public async Task<AdminTestimonialDto> UpdateAsync(long id, TestimonialUpsert req, CancellationToken ct = default)
    {
        var t = await Find(id, ct);
        t.Name = req.Name.Trim(); t.RoleOrCompany = req.RoleOrCompany?.Trim();
        t.Quote = req.Quote.Trim(); t.Rating = Clamp(req.Rating);
        t.PhotoUrl = string.IsNullOrWhiteSpace(req.PhotoUrl) ? null : req.PhotoUrl.Trim();
        t.DisplayOrder = req.DisplayOrder; t.IsActive = req.IsActive; t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToAdmin(t);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var t = await Find(id, ct);
        _db.Testimonials.Remove(t);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetPhotoAsync(long id, byte[] data, string contentType, CancellationToken ct = default)
    {
        var t = await Find(id, ct);
        t.PhotoData = data;
        t.PhotoContentType = contentType;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Data, string ContentType)?> GetPhotoAsync(long id, CancellationToken ct = default)
    {
        var t = await _db.Testimonials.AsNoTracking()
            .Where(x => x.TenantId == Tenant && x.TestimonialId == id && x.PhotoData != null)
            .Select(x => new { x.PhotoData, x.PhotoContentType })
            .FirstOrDefaultAsync(ct);
        return t?.PhotoData is null ? null : (t.PhotoData, t.PhotoContentType ?? "image/jpeg");
    }

    private async Task<Testimonial> Find(long id, CancellationToken ct) =>
        await _db.Testimonials.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.TestimonialId == id, ct)
        ?? throw new AppException("Testimonial not found.", 404);

    private static byte Clamp(byte rating) => rating is < 1 or > 5 ? (byte)5 : rating;

    private static string? ResolvePhoto(long id, bool hasUpload, string? photoUrl, DateTime stamp) =>
        hasUpload ? $"/api/cms/testimonials/{id}/photo?v={stamp.Ticks}" : photoUrl;

    private static AdminTestimonialDto ToAdmin(Testimonial t) => new(
        t.TestimonialId, t.Name, t.RoleOrCompany, t.Quote, t.Rating,
        ResolvePhoto(t.TestimonialId, t.PhotoData != null, t.PhotoUrl, t.UpdatedAt ?? t.CreatedAt),
        t.PhotoData != null, t.DisplayOrder, t.IsActive);
}
