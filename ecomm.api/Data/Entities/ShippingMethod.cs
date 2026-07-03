namespace ecomm.api.Data.Entities;

public class ShippingMethod : ITenantScoped
{
    public long ShippingMethodId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RateType { get; set; } = "Flat";   // Flat | Free | Weight | Zone
    public decimal BaseRate { get; set; }
    public decimal? FreeShippingThreshold { get; set; }
    public int? EstimatedDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
