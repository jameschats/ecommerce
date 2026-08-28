using ecomm.api.Common.Tenancy;

namespace ecomm.api.Data.Entities;

/// <summary>An email captured from the storefront newsletter signup. One row per email per store.
/// If the email also belongs to a customer account, that account's marketing-consent flag is set too.</summary>
public class NewsletterSubscriber : ITenantScoped
{
    public long NewsletterSubscriberId { get; set; }
    public long TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Source { get; set; }   // where they signed up (e.g. "storefront")
    public DateTime CreatedAt { get; set; }
}
