namespace ecomm.api.Data.Entities;

public class Theme : ITenantScoped
{
    public long ThemeId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ThemeSetting> Settings { get; set; } = new List<ThemeSetting>();
}
