namespace ecomm.api.Data.Entities;

/// <summary>
/// A merchant-maintained question and answer. Rendered on the storefront FAQ page and,
/// from C4, the support bot's primary retrieval corpus — a merchant's own words about their
/// own store are better grounding than generic boilerplate.
/// </summary>
public class Faq : ITenantScoped
{
    public long FaqId { get; set; }
    public long TenantId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
