namespace ecomm.api.Data.Entities;

public class Cart : ITenantScoped
{
    public long CartId { get; set; }
    public long TenantId { get; set; } = 1;
    public long? UserId { get; set; }
    public string? SessionId { get; set; }
    public string Status { get; set; } = "Active";   // Active | Converted | Abandoned
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
