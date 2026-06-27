namespace ecomm.api.Data.Entities;

public class AuthProvider
{
    public long AuthProviderId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Provider { get; set; } = string.Empty;   // EmailPassword | MobileOtp | Google
    public bool IsEnabled { get; set; } = true;
    public bool AllowRegistration { get; set; } = true;
    public string? DisplayName { get; set; }
    public int DisplayOrder { get; set; }
    public string? ConfigJson { get; set; }                 // public config only (e.g. Google client-id)
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
