namespace ecomm.api.Data.Entities;

/// <summary>A store legal/policy page (refund, privacy, terms, shipping, contact, legal). Body HTML sanitized on save.</summary>
public class StorePolicy : ITenantScoped
{
    public long StorePolicyId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Handle { get; set; } = string.Empty;   // refund | privacy | terms | shipping | contact | legal
    public string Title { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
