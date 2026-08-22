namespace ecomm.api.Data.Entities;

/// <summary>A merchant-defined date window with a price bias (e.g. +10% during a peak-demand
/// festival, -15% for a planned clearance window) — one of the pricing engine's three signals.
/// Null <see cref="CategoryId"/> applies to every product.</summary>
public class PricingSeasonRule : ITenantScoped
{
    public long PricingSeasonRuleId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal BiasPercent { get; set; }
    public long? CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }
}
