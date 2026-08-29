namespace ecomm.api.Data.Entities;

/// <summary>
/// A per-installation key/value setting for an app (e.g. Low-Stock Alerts' threshold + recipient).
/// Tenant-scoped and keyed by <see cref="AppInstallationId"/>, so each store configures each app it
/// installs independently. Generic on purpose — reusable by any first-party app.
/// </summary>
public class AppSetting : ITenantScoped
{
    public long AppSettingId { get; set; }
    public long TenantId { get; set; }
    public long AppInstallationId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
