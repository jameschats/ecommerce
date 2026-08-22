namespace ecomm.api.Data.Entities;

/// <summary>A third-party integrator's credential for the public API (v4 Phase 6 Track A) — never
/// the internal admin API. Stored as a one-way hash (like refresh tokens), never the raw value —
/// the raw key is shown to the merchant exactly once, at creation.</summary>
public class ApiKey : ITenantScoped
{
    public long ApiKeyId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Label { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    /// <summary>First few characters of the raw key, kept in the clear so a merchant can recognize
    /// which key is which in a list without ever seeing the full value again.</summary>
    public string KeyPrefix { get; set; } = string.Empty;
    /// <summary>Comma-separated scope keys, e.g. "products:read,orders:read,inventory:write".</summary>
    public string Scopes { get; set; } = string.Empty;
    public long? CreatedByUserId { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
