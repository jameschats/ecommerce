namespace ecomm.api.Data.Entities;

/// <summary>
/// A merchant store on the platform. V1 runs a single default tenant (TenantId = 1);
/// V2 activates many. `TenantId` is a bigint PK (not a GUID) — see design-v2.md §3.
/// </summary>
public class Tenant
{
    public long TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Slug { get; set; }              // subdomain lookup key (unique)
    public string? CustomDomain { get; set; }      // post-GA
    public int? PlanId { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
