namespace ecomm.api.Data.Entities;

public class OtpVerification
{
    public long OtpVerificationId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Identifier { get; set; } = string.Empty;  // phone or email
    public string Channel { get; set; } = string.Empty;     // SMS | Email
    public string Purpose { get; set; } = string.Empty;     // Login | Register | ResetPassword | VerifyEmail
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
