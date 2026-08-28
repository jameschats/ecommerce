namespace ecomm.api.Data.Entities;

/// <summary>
/// The PLATFORM's transactional-email (SMTP) config. Single row, platform-level (NOT tenant-scoped).
/// When present it overrides the app-wide <c>Email</c> env config, so the SMTP provider/credentials
/// can be set from the super-admin console instead of editing api.env. Mirrors
/// <see cref="PlatformPaymentSetting"/>. Password encrypted at rest.
/// </summary>
public class PlatformEmailSetting
{
    public byte PlatformEmailSettingId { get; set; } = 1;
    public string Provider { get; set; } = "Logging";   // Logging (mock) | Smtp (live)
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }                // encrypted at rest
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    public bool UseSsl { get; set; } = true;
    public DateTime? UpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
