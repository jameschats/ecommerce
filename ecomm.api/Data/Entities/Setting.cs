namespace ecomm.api.Data.Entities;

public class Setting : ITenantScoped
{
    public long SettingId { get; set; }
    public long TenantId { get; set; } = 1;
    public string SettingKey { get; set; } = string.Empty;
    public string? SettingValue { get; set; }
    public string DataType { get; set; } = "string";
    public string? Category { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
