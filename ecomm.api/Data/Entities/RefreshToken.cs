namespace ecomm.api.Data.Entities;

public class RefreshToken
{
    public long RefreshTokenId { get; set; }
    public long UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
