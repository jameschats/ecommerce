namespace ecomm.api.Data.Entities;

/// <summary>A customer product review + star rating. Moderated (IsApproved) before it shows publicly.</summary>
public class Review : ITenantScoped
{
    public long ReviewId { get; set; }
    public long TenantId { get; set; } = 1;
    public long ProductId { get; set; }
    public long UserId { get; set; }
    public long? OrderId { get; set; }
    public byte Rating { get; set; }
    public string? Title { get; set; }
    public string? Comment { get; set; }
    public bool IsApproved { get; set; }
    public bool IsVerifiedPurchase { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
