namespace ecomm.api.Data.Entities;

/// <summary>Per-tenant key/value config (StoreName, SenderEmail, CurrencyCode, TimeZone, ...).</summary>
public class TenantSetting : ITenantScoped
{
    public long TenantSettingId { get; set; }
    public long TenantId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
