using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Catalog.Services;

public interface IProductImageBackfillService
{
    /// <summary>
    /// One-time maintenance action: watermarks every product image already on disk that
    /// predates the watermarking feature. Safe to re-run — a file already backed up is left
    /// alone rather than watermarked a second time.
    /// </summary>
    Task<Media.BackfillWatermarksResult> BackfillAsync(CancellationToken ct = default);
}

public sealed class ProductImageBackfillService : IProductImageBackfillService
{
    private readonly Data.Context.EcommerceDbContext _db;
    private readonly Media.IImageWatermarkService _watermark;
    private readonly Media.MediaOptions _opts;
    private readonly string _root;
    private readonly ILogger<ProductImageBackfillService> _log;

    public ProductImageBackfillService(
        Data.Context.EcommerceDbContext db,
        Media.IImageWatermarkService watermark,
        IOptions<Media.MediaOptions> opts,
        IHostEnvironment env,
        ILogger<ProductImageBackfillService> log)
    {
        _db = db;
        _watermark = watermark;
        _opts = opts.Value;
        _root = Path.IsPathRooted(_opts.UploadPath)
            ? _opts.UploadPath
            : Path.Combine(env.ContentRootPath, _opts.UploadPath);
        _log = log;
    }

    public async Task<Media.BackfillWatermarksResult> BackfillAsync(CancellationToken ct = default)
    {
        var urls = await _db.ProductImages.Select(i => i.Url).Distinct().ToListAsync(ct);
        var prefix = $"{_opts.PublicBaseUrl}{_opts.RequestPath}/";

        int candidates = 0, done = 0, already = 0, failed = 0;
        var failedUrls = new List<string>();

        foreach (var url in urls)
        {
            var idx = string.IsNullOrWhiteSpace(url) ? -1 : url.IndexOf(prefix, StringComparison.Ordinal);
            if (idx < 0) continue; // Not one of our locally-stored files (e.g. an external URL) — leave it alone.
            candidates++;

            try
            {
                // yyyy/MM/filename.ext, exactly as LocalDiskStorage laid it out.
                var relative = url[(idx + prefix.Length)..].Replace('/', Path.DirectorySeparatorChar);
                var absPath = Path.Combine(_root, relative);
                // Original, unwatermarked bytes kept here before the live file is overwritten —
                // its presence is also this method's idempotency check, so re-running the
                // backfill can never watermark an already-watermarked file a second time.
                var backupPath = Path.Combine(_root, "_originals-backup", relative);

                if (File.Exists(backupPath)) { already++; continue; }
                if (!File.Exists(absPath)) { failed++; failedUrls.Add(url); continue; }

                var bytes = await File.ReadAllBytesAsync(absPath, ct);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                await File.WriteAllBytesAsync(backupPath, bytes, ct);

                var marked = _watermark.Apply(bytes, ContentTypeFor(Path.GetExtension(absPath)));
                await File.WriteAllBytesAsync(absPath, marked, ct);
                done++;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to backfill watermark for {Url}", url);
                failed++;
                failedUrls.Add(url);
            }
        }

        return new Media.BackfillWatermarksResult(candidates, done, already, failed, failedUrls);
    }

    private static string ContentTypeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/jpeg",
    };
}
