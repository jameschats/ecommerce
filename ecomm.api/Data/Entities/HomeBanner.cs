namespace ecomm.api.Data.Entities;

/// <summary>A hero banner shown in the storefront home carousel. Admin-managed.</summary>
public class HomeBanner
{
    public long HomeBannerId { get; set; }
    public long TenantId { get; set; } = 1;
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? CtaText { get; set; }
    public string? LinkUrl { get; set; }
    public string? ImageUrl { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
