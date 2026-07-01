namespace ecomm.api.Data.Entities;

/// <summary>A record of an uploaded media file (image). The bytes live on disk (or a CDN);
/// this row holds the metadata + public URL. See <c>IMediaStorage</c>.</summary>
public class MediaFile
{
    public long MediaFileId { get; set; }
    public long TenantId { get; set; } = 1;
    public long? MediaFolderId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? OriginalName { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public string Url { get; set; } = string.Empty;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
