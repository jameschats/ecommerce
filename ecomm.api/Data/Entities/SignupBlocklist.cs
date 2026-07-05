namespace ecomm.api.Data.Entities;

/// <summary>Blocks re-signup of bad actors by email/GSTIN/phone (platform-global; NOT tenant-scoped).</summary>
public class SignupBlocklist
{
    public long SignupBlocklistId { get; set; }
    public string Type { get; set; } = "Email";   // Email | Gstin | Phone
    public string Value { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public long? CreatedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; }
}
