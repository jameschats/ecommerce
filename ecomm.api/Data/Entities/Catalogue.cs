namespace ecomm.api.Data.Entities;

/// <summary>A downloadable PDF listed on the storefront's Catalogues page — bulk design
/// booklets ("2027 Lotus 10x15 Fancy Cutting Calendar.pdf" etc.), not tied to any one product.
/// The file itself lives on disk via IMediaStorage; this row is just title + where to find it.</summary>
public class Catalogue
{
    public long CatalogueId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    /// <summary>Original uploaded filename — used as the browser's suggested download name,
    /// since the stored file on disk is renamed to a GUID.</summary>
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
