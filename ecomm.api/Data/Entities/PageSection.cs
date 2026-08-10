namespace ecomm.api.Data.Entities;

public class PageSection
{
    public long PageSectionId { get; set; }
    public long PageId { get; set; }
    public string SectionType { get; set; } = string.Empty;  // Banner|Categories|FeaturedProducts|NewArrivals|BestSellers|CustomHtml
    public string? Title { get; set; }

    /// <summary>
    /// Sanitised HTML for Prose and Faq; a small JSON payload for Stats, Cards and Cta (055).
    /// Null for the home page's section types, which render from their own data.
    /// </summary>
    public string? Content { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Page? Page { get; set; }
}
