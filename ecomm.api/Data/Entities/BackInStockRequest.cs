using ecomm.api.Common.Tenancy;

namespace ecomm.api.Data.Entities;

/// <summary>
/// A shopper's request to be emailed when an out-of-stock product is available again. Opt-in and
/// transactional (a direct response to their explicit "notify me"). One un-notified row per
/// email+product; <see cref="NotifiedAt"/> is stamped when the back-in-stock email is sent.
/// </summary>
public class BackInStockRequest : ITenantScoped
{
    public long BackInStockRequestId { get; set; }
    public long TenantId { get; set; }
    public long ProductId { get; set; }
    public string Email { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? NotifiedAt { get; set; }
}
