using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Plans;
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
    /// <summary>Generates responsive WebP variants for every already-uploaded image that doesn't have
    /// them yet — for uploads made before ImageVariantService existed. Safe to re-run.</summary>
    Task<BackfillResultDto> BackfillVariantsAsync(CancellationToken ct = default);
}

public sealed record BackfillResultDto(int Processed, int Succeeded, int Skipped);

public sealed class MediaService : IMediaService
{
    private long Tenant => _db.CurrentTenantId;
    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];

    private readonly IMediaStorage _storage;
    private readonly EcommerceDbContext _db;
    private readonly MediaOptions _opts;
    private readonly IEntitlementService _entitlements;
    private readonly IImageVariantService _variants;

    public MediaService(IMediaStorage storage, EcommerceDbContext db, IOptions<MediaOptions> opts, IEntitlementService entitlements, IImageVariantService variants)
    {
        _storage = storage;
        _db = db;
        _opts = opts.Value;
        _entitlements = entitlements;
        _variants = variants;
    }

    public async Task<MediaDto> UploadAsync(IFormFile file, long? userId, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) throw new AppException("Please choose a non-empty image file.");
        if (file.Length > _opts.MaxBytes) throw new AppException($"Image must be {_opts.MaxBytes / (1024 * 1024)} MB or smaller.");
        var type = file.ContentType?.ToLowerInvariant() ?? "";
        if (!AllowedTypes.Contains(type)) throw new AppException("Only JPEG, PNG, WebP or GIF images are allowed.");
        await _entitlements.EnsureCanUploadAsync(file.Length, ct);

        // Buffered once so both the original save and the (best-effort) variant generation can each
        // read the bytes from the start — IFormFile's own stream can only be consumed once.
        using var buffer = new MemoryStream();
        await using (var stream = file.OpenReadStream())
            await stream.CopyToAsync(buffer, ct);

        buffer.Position = 0;
        var stored = await _storage.SaveAsync(buffer, file.FileName, type, ct);

        if (type != "image/gif") // animated GIFs would just get their first frame resized — skip, original still serves fine
        {
            buffer.Position = 0;
            await _variants.GenerateAsync(stored.Url, buffer, ct);
        }

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

    public async Task<BackfillResultDto> BackfillVariantsAsync(CancellationToken ct = default)
    {
        var files = await _db.MediaFiles
            .Where(m => m.MimeType != null && m.MimeType != "image/gif")
            .Select(m => m.Url)
            .ToListAsync(ct);

        int succeeded = 0, skipped = 0;
        foreach (var url in files)
        {
            await using var data = await _storage.OpenReadAsync(url, ct);
            if (data is null) { skipped++; continue; }
            await _variants.GenerateAsync(url, data, ct);
            succeeded++;
        }
        return new BackfillResultDto(files.Count, succeeded, skipped);
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
