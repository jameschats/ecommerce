namespace ecomm.api.Data.Entities;

public class ProductImage
{
    public long ProductImageId { get; set; }
    public long ProductId { get; set; }
    public long? MediaFileId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }

    public Product? Product { get; set; }
}
