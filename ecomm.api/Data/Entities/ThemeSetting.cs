namespace ecomm.api.Data.Entities;

public class ThemeSetting
{
    public long ThemeSettingId { get; set; }
    public long ThemeId { get; set; }
    public string SettingKey { get; set; } = string.Empty;   // PrimaryColor | SecondaryColor | Font | ButtonStyle | Logo
    public string? SettingValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Theme? Theme { get; set; }
}
