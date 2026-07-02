namespace ecomm.api.Data.Entities;

/// <summary>An in-app notification (bell feed). Per-user for customers; shared for admins (UserId null).</summary>
public class Notification
{
    public long NotificationId { get; set; }
    public long TenantId { get; set; } = 1;
    public long? UserId { get; set; }
    public string Audience { get; set; } = "Customer"; // Customer | Admin
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
