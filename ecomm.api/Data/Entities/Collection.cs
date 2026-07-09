namespace ecomm.api.Data.Entities;

/// <summary>
/// A merchandising group of products (distinct from a Category taxonomy). Membership is either
/// Manual (via <see cref="ProductCollection"/> rows) or Automated (products matching <see cref="RulesJson"/>).
/// </summary>
public class Collection : ITenantScoped
{
    public long CollectionId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string CollectionType { get; set; } = "Manual";   // Manual | Automated
    public string MatchType { get; set; } = "All";           // All | Any (automated)
    public string? RulesJson { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Manual membership of a product in a collection. Scoped via its parent collection.</summary>
public class ProductCollection : ITenantScoped
{
    public long ProductCollectionId { get; set; }
    public long TenantId { get; set; } = 1;
    public long CollectionId { get; set; }
    public long ProductId { get; set; }
    public int DisplayOrder { get; set; }
}
