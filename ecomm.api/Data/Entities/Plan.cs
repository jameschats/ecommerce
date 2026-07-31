namespace ecomm.api.Data.Entities;

/// <summary>A platform subscription plan sold to merchants. Global (not tenant-scoped).</summary>
public class Plan
{
    public int PlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public decimal? IntroPriceInr { get; set; }   // promo price for the first IntroMonths paid cycles (migration 176)
    public int? IntroMonths { get; set; }
    public DateTime? IntroEndsAt { get; set; }    // campaign deadline: closes the offer to NEW joiners (migration 177)
    public int? MaxProducts { get; set; }   // null = unlimited
    public int? MaxOrders { get; set; }     // null = unlimited (per month)
    public int? MaxStorageMb { get; set; }  // null = unlimited
    public int AiCredits { get; set; }
    public string? Features { get; set; }   // JSON
    public string? MarketingEngineLevel { get; set; }   // free-text pricing-page label, e.g. "No"/"Yes"/"Advanced" — not gated
    public string? LiveChatLevel { get; set; }
    public string? HelpdeskLevel { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
