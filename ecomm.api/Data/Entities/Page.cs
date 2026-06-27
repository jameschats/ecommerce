namespace ecomm.api.Data.Entities;

public class Page
{
    public long PageId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Type { get; set; } = "Custom";   // Home | Custom
    public bool IsPublished { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<PageSection> Sections { get; set; } = new List<PageSection>();
}
