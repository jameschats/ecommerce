namespace ecomm.api.Features.Media;

/// <summary>The outcome of persisting an uploaded file: its public URL + basic metadata.</summary>
public sealed record StoredFile(string Url, string StoredName, long Size);

/// <summary>
/// Abstraction over where uploaded media bytes live. <see cref="LocalDiskStorage"/> writes to a
/// local folder served at <c>/uploads</c>; a future <c>R2Storage</c>/<c>S3Storage</c> would push to
/// object storage and return a CDN URL — same interface, no caller changes.
/// </summary>
public interface IMediaStorage
{
    Task<StoredFile> SaveAsync(Stream data, string originalName, string contentType, CancellationToken ct = default);

    /// <summary>Saves a derived file (e.g. a resized WebP) alongside an already-stored original, using
    /// the original's own name as the stem — e.g. original url ".../abc123.jpg" + suffix "-400w.webp"
    /// writes ".../abc123-400w.webp". Lets callers build deterministic srcset URLs client-side with no
    /// DB/DTO changes. Returns false (never throws) if the original can't be located.</summary>
    Task<bool> SaveVariantAsync(string originalUrl, string suffix, Stream data, CancellationToken ct = default);

    /// <summary>Reopens a previously-stored file for reading (e.g. to backfill variants for uploads
    /// that predate <see cref="SaveVariantAsync"/>). Returns null if the URL isn't one of ours or the
    /// file no longer exists.</summary>
    Task<Stream?> OpenReadAsync(string url, CancellationToken ct = default);
}
