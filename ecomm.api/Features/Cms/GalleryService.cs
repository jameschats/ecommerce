using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Cms;

/// <summary>What the storefront renders per gallery photo (image already resolved to a URL).</summary>
public sealed record GalleryImageDto(long GalleryImageId, string? ImageUrl, string? Title, string? Link);

/// <summary>Full gallery row for the admin editor.</summary>
public sealed record AdminGalleryImageDto(
    long GalleryImageId, string Section, string? Title, string? LinkUrl, string? ImageUrl, bool HasUpload, int DisplayOrder, bool IsActive);

public sealed record GalleryImageUpsert(string Section, string? Title, string? LinkUrl, string? ImageUrl, int DisplayOrder, bool IsActive);

public sealed record SectionTitleUpsert(string Title);

public interface IGalleryService
{
    Task<List<GalleryImageDto>> GetActiveAsync(string section, CancellationToken ct = default);
    Task<List<AdminGalleryImageDto>> GetAllAsync(string section, CancellationToken ct = default);
    Task<AdminGalleryImageDto> CreateAsync(GalleryImageUpsert req, CancellationToken ct = default);
    Task<AdminGalleryImageDto> UpdateAsync(long id, GalleryImageUpsert req, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task SetImageAsync(long id, byte[] data, string contentType, CancellationToken ct = default);
    Task<(byte[] Data, string ContentType)?> GetImageAsync(long id, CancellationToken ct = default);
    Task<string> GetSectionTitleAsync(string section, CancellationToken ct = default);
    Task SetSectionTitleAsync(string section, string title, CancellationToken ct = default);

    /// <summary>
    /// One-time: watermarks every gallery photo already stored from before watermarking
    /// existed. Idempotent — a photo already backed up is left alone rather than watermarked
    /// a second time.
    /// </summary>
    Task<Media.BackfillWatermarksResult> BackfillWatermarksAsync(CancellationToken ct = default);

    /// <summary>
    /// One-time: shrinks every gallery photo already stored at full upload resolution, from
    /// before uploads were resized. Idempotent — a photo already at or under maxWidth is left
    /// alone.
    /// </summary>
    Task<Media.BackfillResizeResult> BackfillResizeAsync(int maxWidth, CancellationToken ct = default);
}

public sealed class GalleryService : IGalleryService
{
    private const long Tenant = 1;
    /// <summary>Keeps a stray/typo query param from splintering photos into an unmanaged section.</summary>
    public static readonly string[] KnownSections = ["new-designs", "featured"];

    /// <summary>Shown until an admin sets a Gallery.Title.{section} setting for that section.</summary>
    private static readonly Dictionary<string, string> DefaultTitles = new()
    {
        ["new-designs"] = "New designs",
        ["featured"] = "Our Work",
    };

    private readonly EcommerceDbContext _db;
    private readonly Media.IImageWatermarkService _watermark;
    private readonly string _backupRoot;
    private readonly ILogger<GalleryService> _log;

    public GalleryService(EcommerceDbContext db, Media.IImageWatermarkService watermark,
        IOptions<Media.MediaOptions> mediaOpts, IHostEnvironment env, ILogger<GalleryService> log)
    {
        _db = db;
        _watermark = watermark;
        var opts = mediaOpts.Value;
        var root = Path.IsPathRooted(opts.UploadPath) ? opts.UploadPath : Path.Combine(env.ContentRootPath, opts.UploadPath);
        _backupRoot = Path.Combine(root, "_gallery-originals-backup");
        _log = log;
    }

    public static string NormalizeSection(string? section) =>
        section is not null && KnownSections.Contains(section) ? section : "new-designs";

    public async Task<string> GetSectionTitleAsync(string section, CancellationToken ct = default)
    {
        section = NormalizeSection(section);
        var stored = await _db.Settings
            .Where(s => s.TenantId == Tenant && s.SettingKey == $"Gallery.Title.{section}")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(stored) ? DefaultTitles[section] : stored;
    }

    public async Task SetSectionTitleAsync(string section, string title, CancellationToken ct = default)
    {
        section = NormalizeSection(section);
        var key = $"Gallery.Title.{section}";
        var row = await _db.Settings.FirstOrDefaultAsync(s => s.TenantId == Tenant && s.SettingKey == key, ct);
        var value = title.Trim();
        if (row is null)
            _db.Settings.Add(new Setting { TenantId = Tenant, SettingKey = key, SettingValue = value, CreatedAt = DateTime.UtcNow });
        else
            row.SettingValue = value;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<GalleryImageDto>> GetActiveAsync(string section, CancellationToken ct = default)
    {
        section = NormalizeSection(section);
        var rows = await _db.GalleryImages.AsNoTracking()
            .Where(g => g.TenantId == Tenant && g.Section == section && g.IsActive)
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.GalleryImageId)
            .Select(g => new { g.GalleryImageId, g.Title, g.LinkUrl, g.ImageUrl, HasUpload = g.ImageData != null, g.UpdatedAt, g.CreatedAt })
            .ToListAsync(ct);

        return rows.Select(g => new GalleryImageDto(
            g.GalleryImageId, ResolveImage(g.GalleryImageId, g.HasUpload, g.ImageUrl, g.UpdatedAt ?? g.CreatedAt),
            g.Title, g.LinkUrl)).ToList();
    }

    public async Task<List<AdminGalleryImageDto>> GetAllAsync(string section, CancellationToken ct = default)
    {
        section = NormalizeSection(section);
        var rows = await _db.GalleryImages.AsNoTracking()
            .Where(g => g.TenantId == Tenant && g.Section == section)
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.GalleryImageId)
            .Select(g => new { g.GalleryImageId, g.Section, g.Title, g.LinkUrl, g.ImageUrl, HasUpload = g.ImageData != null, g.DisplayOrder, g.IsActive, g.UpdatedAt, g.CreatedAt })
            .ToListAsync(ct);

        return rows.Select(g => new AdminGalleryImageDto(
            g.GalleryImageId, g.Section, g.Title, g.LinkUrl,
            ResolveImage(g.GalleryImageId, g.HasUpload, g.ImageUrl, g.UpdatedAt ?? g.CreatedAt),
            g.HasUpload, g.DisplayOrder, g.IsActive)).ToList();
    }

    public async Task<AdminGalleryImageDto> CreateAsync(GalleryImageUpsert req, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var g = new GalleryImage
        {
            TenantId = Tenant, Section = NormalizeSection(req.Section), Title = req.Title, LinkUrl = req.LinkUrl,
            ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim(),
            DisplayOrder = req.DisplayOrder, IsActive = req.IsActive, CreatedAt = now,
        };
        _db.GalleryImages.Add(g);
        await _db.SaveChangesAsync(ct);
        return ToAdmin(g);
    }

    public async Task<AdminGalleryImageDto> UpdateAsync(long id, GalleryImageUpsert req, CancellationToken ct = default)
    {
        var g = await Find(id, ct);
        g.Title = req.Title; g.LinkUrl = req.LinkUrl;
        g.ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim();
        g.DisplayOrder = req.DisplayOrder; g.IsActive = req.IsActive; g.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToAdmin(g);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var g = await Find(id, ct);
        _db.GalleryImages.Remove(g);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetImageAsync(long id, byte[] data, string contentType, CancellationToken ct = default)
    {
        var g = await Find(id, ct);
        g.ImageData = data;
        g.ImageContentType = contentType;
        g.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Data, string ContentType)?> GetImageAsync(long id, CancellationToken ct = default)
    {
        var g = await _db.GalleryImages.AsNoTracking()
            .Where(x => x.TenantId == Tenant && x.GalleryImageId == id && x.ImageData != null)
            .Select(x => new { x.ImageData, x.ImageContentType })
            .FirstOrDefaultAsync(ct);
        return g?.ImageData is null ? null : (g.ImageData, g.ImageContentType ?? "image/jpeg");
    }

    public async Task<Media.BackfillWatermarksResult> BackfillWatermarksAsync(CancellationToken ct = default)
    {
        var rows = await _db.GalleryImages
            .Where(g => g.TenantId == Tenant && g.ImageData != null)
            .Select(g => new { g.GalleryImageId, g.ImageContentType })
            .ToListAsync(ct);

        int done = 0, already = 0, failed = 0;
        var failedIds = new List<string>();
        Directory.CreateDirectory(_backupRoot);

        foreach (var row in rows)
        {
            var ext = ExtensionFor(row.ImageContentType);
            var backupPath = Path.Combine(_backupRoot, $"{row.GalleryImageId}{ext}");
            if (File.Exists(backupPath)) { already++; continue; }

            try
            {
                var g = await _db.GalleryImages.FirstAsync(x => x.GalleryImageId == row.GalleryImageId, ct);
                var original = g.ImageData!;
                var contentType = g.ImageContentType ?? "image/jpeg";

                await File.WriteAllBytesAsync(backupPath, original, ct);
                g.ImageData = _watermark.Apply(original, contentType);
                g.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                done++;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to backfill watermark for gallery image {Id}", row.GalleryImageId);
                failed++;
                failedIds.Add(row.GalleryImageId.ToString());
            }
        }

        return new Media.BackfillWatermarksResult(rows.Count, done, already, failed, failedIds);
    }

    public async Task<Media.BackfillResizeResult> BackfillResizeAsync(int maxWidth, CancellationToken ct = default)
    {
        var rows = await _db.GalleryImages
            .Where(g => g.TenantId == Tenant && g.ImageData != null)
            .Select(g => new { g.GalleryImageId, g.ImageContentType })
            .ToListAsync(ct);

        int done = 0, alreadySmall = 0, failed = 0;
        long bytesBefore = 0, bytesAfter = 0;
        var failedIds = new List<string>();
        Directory.CreateDirectory(_backupRoot);

        foreach (var row in rows)
        {
            try
            {
                var g = await _db.GalleryImages.FirstAsync(x => x.GalleryImageId == row.GalleryImageId, ct);
                var original = g.ImageData!;
                var contentType = g.ImageContentType ?? "image/jpeg";

                // Same backup folder the watermark backfill uses — a photo that already went
                // through that backfill already has its pre-processing original saved here.
                var backupPath = Path.Combine(_backupRoot, $"{row.GalleryImageId}{ExtensionFor(contentType)}");
                if (!File.Exists(backupPath)) await File.WriteAllBytesAsync(backupPath, original, ct);

                var resized = _watermark.ResizeIfLarger(original, contentType, maxWidth);
                if (resized.Length >= original.Length) { alreadySmall++; continue; }

                bytesBefore += original.Length;
                bytesAfter += resized.Length;
                g.ImageData = resized;
                g.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                done++;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to backfill resize for gallery image {Id}", row.GalleryImageId);
                failed++;
                failedIds.Add(row.GalleryImageId.ToString());
            }
        }

        return new Media.BackfillResizeResult(rows.Count, done, alreadySmall, failed, failedIds, bytesBefore, bytesAfter);
    }

    private static string ExtensionFor(string? contentType) => contentType?.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        _ => ".jpg",
    };

    private async Task<GalleryImage> Find(long id, CancellationToken ct) =>
        await _db.GalleryImages.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.GalleryImageId == id, ct)
        ?? throw new AppException("Gallery image not found.", 404);

    private static string? ResolveImage(long id, bool hasUpload, string? imageUrl, DateTime stamp) =>
        hasUpload ? $"/api/cms/gallery/{id}/image?v={stamp.Ticks}" : imageUrl;

    private static AdminGalleryImageDto ToAdmin(GalleryImage g) => new(
        g.GalleryImageId, g.Section, g.Title, g.LinkUrl,
        ResolveImage(g.GalleryImageId, g.ImageData != null, g.ImageUrl, g.UpdatedAt ?? g.CreatedAt),
        g.ImageData != null, g.DisplayOrder, g.IsActive);
}
