using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Media;

/// <summary>Writes uploads to a local folder (served at <c>Media:RequestPath</c>). The default
/// <see cref="IMediaStorage"/> for the single-VPS deployment. Files are laid out under
/// <c>{UploadPath}/{yyyy}/{MM}/{guid}.{ext}</c> to avoid huge flat directories.</summary>
public sealed class LocalDiskStorage : IMediaStorage
{
    private readonly MediaOptions _opts;
    private readonly string _root;
    private readonly IImageWatermarkService _watermark;

    public LocalDiskStorage(IOptions<MediaOptions> opts, IHostEnvironment env, IImageWatermarkService watermark)
    {
        _opts = opts.Value;
        _root = Path.IsPathRooted(_opts.UploadPath)
            ? _opts.UploadPath
            : Path.Combine(env.ContentRootPath, _opts.UploadPath);
        _watermark = watermark;
    }

    public async Task<StoredFile> SaveAsync(Stream data, string originalName, string contentType, bool watermark = false, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var ext = Extension(originalName, contentType);
        var relDir = $"{now:yyyy}/{now:MM}";
        var storedName = $"{Guid.NewGuid():N}{ext}";

        var absDir = Path.Combine(_root, now.ToString("yyyy"), now.ToString("MM"));
        Directory.CreateDirectory(absDir);
        var absPath = Path.Combine(absDir, storedName);

        if (watermark)
        {
            using var buffer = new MemoryStream();
            await data.CopyToAsync(buffer, ct);
            var marked = _watermark.Apply(buffer.ToArray(), contentType);
            await File.WriteAllBytesAsync(absPath, marked, ct);
        }
        else
        {
            await using var fs = new FileStream(absPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await data.CopyToAsync(fs, ct);
        }

        var size = new FileInfo(absPath).Length;
        var url = $"{_opts.PublicBaseUrl}{_opts.RequestPath}/{relDir}/{storedName}";
        return new StoredFile(url, storedName, size);
    }

    private static string Extension(string originalName, string contentType)
    {
        var ext = Path.GetExtension(originalName);
        if (!string.IsNullOrWhiteSpace(ext) && ext.Length <= 6) return ext.ToLowerInvariant();
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg",
        };
    }
}
