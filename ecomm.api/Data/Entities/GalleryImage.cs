namespace ecomm.api.Data.Entities;

/// <summary>A photo in the storefront home page's continuous-scroll gallery strip. Admin-managed.</summary>
public class GalleryImage
{
    public long GalleryImageId { get; set; }
    public long TenantId { get; set; } = 1;

    /// <summary>Which home-page gallery this belongs to: "new-designs" (top, below the banner) or "featured" (bottom, below the price list).</summary>
    public string Section { get; set; } = "new-designs";
    public string? Title { get; set; }
    public string? LinkUrl { get; set; }
    public string? ImageUrl { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
