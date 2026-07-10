using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Media;

/// <summary>What the admin gets back after uploading: the id to persist + the URL to render.</summary>
public sealed record MediaDto(long MediaFileId, string Url, long Size);

/// <summary>A file in the media library, with how many products reference it.</summary>
public sealed record MediaFileDto(long MediaFileId, string Url, string? OriginalName, string? MimeType, long? SizeBytes, int References, DateTime CreatedAt);

public interface IMediaService
{
    Task<MediaDto> UploadAsync(IFormFile file, long? userId, CancellationToken ct = default);
    Task<PagedResult<MediaFileDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class MediaService : IMediaService
{
    private long Tenant => _db.CurrentTenantId;
    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];

    private readonly IMediaStorage _storage;
    private readonly EcommerceDbContext _db;
    private readonly MediaOptions _opts;

    public MediaService(IMediaStorage storage, EcommerceDbContext db, IOptions<MediaOptions> opts)
    {
        _storage = storage;
        _db = db;
        _opts = opts.Value;
    }

    public async Task<MediaDto> UploadAsync(IFormFile file, long? userId, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) throw new AppException("Please choose a non-empty image file.");
        if (file.Length > _opts.MaxBytes) throw new AppException($"Image must be {_opts.MaxBytes / (1024 * 1024)} MB or smaller.");
        var type = file.ContentType?.ToLowerInvariant() ?? "";
        if (!AllowedTypes.Contains(type)) throw new AppException("Only JPEG, PNG, WebP or GIF images are allowed.");

        StoredFile stored;
        await using (var stream = file.OpenReadStream())
            stored = await _storage.SaveAsync(stream, file.FileName, type, ct);

        var media = new MediaFile
        {
            TenantId = Tenant,
            FileName = stored.StoredName,
            OriginalName = file.FileName,
            MimeType = type,
            SizeBytes = stored.Size,
            Url = stored.Url,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
        };
        _db.MediaFiles.Add(media);
        await _db.SaveChangesAsync(ct);

        return new MediaDto(media.MediaFileId, media.Url, stored.Size);
    }

    public async Task<PagedResult<MediaFileDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.MediaFiles.OrderByDescending(m => m.MediaFileId);
        var total = await q.LongCountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new MediaFileDto(m.MediaFileId, m.Url, m.OriginalName, m.MimeType, m.SizeBytes,
                _db.ProductImages.Count(i => i.Url == m.Url), m.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<MediaFileDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var m = await _db.MediaFiles.FirstOrDefaultAsync(x => x.MediaFileId == id, ct)
            ?? throw new AppException("File not found.", 404);
        // Removes the library record; the stored bytes are left in place (harmless orphan).
        _db.MediaFiles.Remove(m);
        await _db.SaveChangesAsync(ct);
    }
}
