namespace ecomm.api.Data.Entities;

/// <summary>A platform subscription plan sold to merchants. Global (not tenant-scoped).</summary>
public class Plan
{
    public int PlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public int? MaxProducts { get; set; }   // null = unlimited
    public int? MaxOrders { get; set; }     // null = unlimited (per month)
    public int AiCredits { get; set; }
    public string? Features { get; set; }   // JSON
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
