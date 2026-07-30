namespace ecomm.api.Data.Entities;

public class Category
{
    public long CategoryId { get; set; }
    public long TenantId { get; set; } = 1;
    public long? ParentCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this category appears in the main price list (home and Order Now).
    /// False for ranges sold from a page of their own, such as Finished Calendar.
    /// </summary>
    public bool ShowInPriceList { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
}
