namespace ecomm.api.Data.Entities;

public class UserExternalLogin
{
    public long UserExternalLoginId { get; set; }
    public long UserId { get; set; }
    public string Provider { get; set; } = string.Empty;       // Google | MobileOtp
    public string ProviderUserId { get; set; } = string.Empty; // Google `sub` / phone number
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}
