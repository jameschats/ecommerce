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
    public string? CustomDomain { get; set; }      // merchant's own domain (unique)
    public bool CustomDomainVerified { get; set; }
    public string? CustomDomainToken { get; set; } // proven at /.well-known during verification
    public int? PlanId { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string Standing { get; set; } = "Good";   // Good | Trusted | Watch | Flagged | Blacklisted
    public string? StandingReason { get; set; }
    public DateTime? StandingUpdatedAt { get; set; }
    public string? PlatformTags { get; set; }        // comma-separated, platform-owner set (migration 173)
    public DateTime? OffboardedAt { get; set; }       // soft off-boarding, distinct from SuspendedAt
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
