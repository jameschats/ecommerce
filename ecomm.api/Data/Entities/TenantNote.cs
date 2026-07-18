namespace ecomm.api.Data.Entities;

/// <summary>Append-only internal note a platform owner writes about a tenant (platform-level; NOT tenant-scoped).</summary>
public class TenantNote
{
    public long TenantNoteId { get; set; }
    public long TenantId { get; set; }
    public long AdminUserId { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
