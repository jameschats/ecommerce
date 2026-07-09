namespace ecomm.api.Data.Entities;

/// <summary>
/// Merchant-managed CRM fields for a customer (a <see cref="User"/> in the CUSTOMER role).
/// Marketing consent + admin notes + tags. Identity/auth stays on <see cref="User"/>.
/// </summary>
public class CustomerProfile : ITenantScoped
{
    public long CustomerProfileId { get; set; }
    public long TenantId { get; set; } = 1;
    public long UserId { get; set; }
    public bool AcceptsEmailMarketing { get; set; }
    public bool AcceptsSmsMarketing { get; set; }
    public bool AcceptsWhatsappMarketing { get; set; }
    public string? Notes { get; set; }
    public string? Tags { get; set; }   // comma-separated
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
