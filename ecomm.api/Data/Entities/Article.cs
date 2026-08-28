namespace ecomm.api.Data.Entities;

/// <summary>
/// A blog / content-marketing article (G5) — the destination the AI blog writer was blocked on.
/// Tenant-scoped; published articles are public on the storefront (/blog) with their own SEO meta and
/// Article JSON-LD, and are listed in the sitemap.
/// </summary>
public class Article : ITenantScoped
{
    public long ArticleId { get; set; }
    public long TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    /// <summary>Article body as sanitised HTML (the editor authors HTML; the AI draft returns HTML).</summary>
    public string BodyHtml { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? AuthorName { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string Status { get; set; } = "Draft";   // Draft | Published
    public DateTime? PublishedAt { get; set; }
    public long? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
