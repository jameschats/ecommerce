using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Cms;

public sealed record CatalogueDto(
    long CatalogueId, string Title, string FileUrl, string FileName, long FileSizeBytes, int DisplayOrder, bool IsActive);

public sealed record CatalogueUpdateRequest(string Title, int DisplayOrder, bool IsActive);

public interface ICatalogueService
{
    Task<List<CatalogueDto>> GetActiveAsync(CancellationToken ct = default);
    Task<List<CatalogueDto>> GetAllAsync(CancellationToken ct = default);
    Task<CatalogueDto> UploadAsync(IFormFile file, CancellationToken ct = default);
    Task<CatalogueDto> UpdateAsync(long id, CatalogueUpdateRequest req, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class CatalogueService : ICatalogueService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;
    private readonly Media.IMediaStorage _storage;

    public CatalogueService(EcommerceDbContext db, Media.IMediaStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public Task<List<CatalogueDto>> GetActiveAsync(CancellationToken ct = default) =>
        Query(activeOnly: true).ToListAsync(ct);

    public Task<List<CatalogueDto>> GetAllAsync(CancellationToken ct = default) =>
        Query(activeOnly: false).ToListAsync(ct);

    public async Task<CatalogueDto> UploadAsync(IFormFile file, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await using var stream = file.OpenReadStream();
        var stored = await _storage.SaveAsync(stream, file.FileName, file.ContentType, watermark: false, ct);

        var maxOrder = await _db.Catalogues.Where(c => c.TenantId == Tenant)
            .Select(c => (int?)c.DisplayOrder).MaxAsync(ct) ?? 0;

        var c = new Catalogue
        {
            TenantId = Tenant,
            // A readable default from the filename — "2027 Lotus 10x15 Fancy Cutting
            // Calendar.pdf" becomes "2027 Lotus 10x15 Fancy Cutting Calendar" — renamed from
            // admin afterwards if wanted.
            Title = Path.GetFileNameWithoutExtension(file.FileName),
            FileUrl = stored.Url,
            FileName = file.FileName,
            FileSizeBytes = stored.Size,
            DisplayOrder = maxOrder + 10,
            IsActive = true,
            CreatedAt = now,
        };
        _db.Catalogues.Add(c);
        await _db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<CatalogueDto> UpdateAsync(long id, CatalogueUpdateRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) throw new AppException("Enter a title.");
        var c = await Find(id, ct);
        c.Title = req.Title.Trim();
        c.DisplayOrder = req.DisplayOrder;
        c.IsActive = req.IsActive;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var c = await Find(id, ct);
        _db.Catalogues.Remove(c);
        await _db.SaveChangesAsync(ct);
        // The disk file itself is left in place — IMediaStorage has no delete, and orphaning a
        // few PDFs costs nothing worth building that for.
    }

    private IQueryable<CatalogueDto> Query(bool activeOnly) =>
        _db.Catalogues.AsNoTracking()
            .Where(c => c.TenantId == Tenant && (!activeOnly || c.IsActive))
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.CatalogueId)
            .Select(c => new CatalogueDto(c.CatalogueId, c.Title, c.FileUrl, c.FileName, c.FileSizeBytes, c.DisplayOrder, c.IsActive));

    private async Task<Catalogue> Find(long id, CancellationToken ct) =>
        await _db.Catalogues.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.CatalogueId == id, ct)
        ?? throw new AppException("Catalogue not found.", 404);

    private static CatalogueDto ToDto(Catalogue c) =>
        new(c.CatalogueId, c.Title, c.FileUrl, c.FileName, c.FileSizeBytes, c.DisplayOrder, c.IsActive);
}
