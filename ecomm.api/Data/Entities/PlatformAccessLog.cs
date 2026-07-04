namespace ecomm.api.Data.Entities;

/// <summary>Audit of every super-admin view/action on a tenant (platform-level; NOT tenant-scoped).</summary>
public class PlatformAccessLog
{
    public long PlatformAccessLogId { get; set; }
    public long AdminUserId { get; set; }
    public long? TenantId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; }
}
