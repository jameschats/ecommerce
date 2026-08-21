namespace ecomm.api.Data.Entities;

/// <summary>One single-use recovery code for a 2FA-enrolled user — the resolved answer to "lost my
/// authenticator device": 10 are generated at enrollment, each usable exactly once as an alternative
/// to a TOTP code. Hashed at rest (never the raw code), same posture as OtpService's OTP codes.</summary>
public class UserTwoFactorBackupCode
{
    public long UserTwoFactorBackupCodeId { get; set; }
    public long UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime? UsedAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}
