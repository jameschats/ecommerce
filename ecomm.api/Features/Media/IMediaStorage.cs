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
}
