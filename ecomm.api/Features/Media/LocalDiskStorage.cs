using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Media;

/// <summary>Writes uploads to a local folder (served at <c>Media:RequestPath</c>). The default
/// <see cref="IMediaStorage"/> for the single-VPS deployment. Files are laid out under
/// <c>{UploadPath}/{yyyy}/{MM}/{guid}.{ext}</c> to avoid huge flat directories.</summary>
public sealed class LocalDiskStorage : IMediaStorage
{
    private readonly MediaOptions _opts;
    private readonly string _root;

    public LocalDiskStorage(IOptions<MediaOptions> opts, IHostEnvironment env)
    {
        _opts = opts.Value;
        _root = Path.IsPathRooted(_opts.UploadPath)
            ? _opts.UploadPath
            : Path.Combine(env.ContentRootPath, _opts.UploadPath);
    }

    public async Task<StoredFile> SaveAsync(Stream data, string originalName, string contentType, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var ext = Extension(originalName, contentType);
        var relDir = $"{now:yyyy}/{now:MM}";
        var storedName = $"{Guid.NewGuid():N}{ext}";

        var absDir = Path.Combine(_root, now.ToString("yyyy"), now.ToString("MM"));
        Directory.CreateDirectory(absDir);
        var absPath = Path.Combine(absDir, storedName);

        await using (var fs = new FileStream(absPath, FileMode.Create, FileAccess.Write, FileShare.None))
            await data.CopyToAsync(fs, ct);

        var size = new FileInfo(absPath).Length;
        var url = $"{_opts.PublicBaseUrl}{_opts.RequestPath}/{relDir}/{storedName}";
        return new StoredFile(url, storedName, size);
    }

    public async Task<bool> SaveVariantAsync(string originalUrl, string suffix, Stream data, CancellationToken ct = default)
    {
        if (!TryMapUrlToAbsolutePath(originalUrl, out var absPath)) return false;
        var dir = Path.GetDirectoryName(absPath);
        if (dir is null) return false;
        Directory.CreateDirectory(dir);
        var variantPath = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(absPath)}{suffix}");
        await using var fs = new FileStream(variantPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await data.CopyToAsync(fs, ct);
        return true;
    }

    public Task<Stream?> OpenReadAsync(string url, CancellationToken ct = default)
    {
        if (!TryMapUrlToAbsolutePath(url, out var absPath) || !File.Exists(absPath))
            return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(new FileStream(absPath, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    /// <summary>Reverses <see cref="SaveAsync"/>'s URL construction — finds the `Media:RequestPath`
    /// marker in the URL and maps whatever comes after it back onto the local upload tree.</summary>
    private bool TryMapUrlToAbsolutePath(string url, out string absPath)
    {
        absPath = "";
        var marker = _opts.RequestPath.TrimEnd('/') + "/";
        var idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;
        var relativeParts = url[(idx + marker.Length)..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (relativeParts.Length == 0) return false;
        absPath = Path.Combine(_root, Path.Combine(relativeParts));
        return true;
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
