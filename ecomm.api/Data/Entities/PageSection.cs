using ecomm.api.Common.Tenancy;

namespace ecomm.api.Data.Entities;

public class PageSection : ITenantScoped
{
    public long PageSectionId { get; set; }
    public long TenantId { get; set; } = 1;
    public long PageId { get; set; }
    public string SectionType { get; set; } = string.Empty;  // Hero|RichText|FeaturedProducts|Categories|ImageWithText|Testimonials|CtaNewsletter|ProductGrid
    public string? Title { get; set; }
    public string? Settings { get; set; }   // JSON — per-section settings (heading, columns, colors, ...)
    public string? Blocks { get; set; }     // JSON — ordered child blocks (hero slides, testimonial items)
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Page? Page { get; set; }
}
