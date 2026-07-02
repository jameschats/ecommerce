namespace ecomm.api.Data.Entities;

public class User
{
    public long UserId { get; set; }
    public long TenantId { get; set; } = 1;
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? PasswordHash { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsPhoneVerified { get; set; }
    public DateTime? PhoneVerifiedAt { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserExternalLogin> ExternalLogins { get; set; } = new List<UserExternalLogin>();
}
