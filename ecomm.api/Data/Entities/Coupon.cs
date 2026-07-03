namespace ecomm.api.Data.Entities;

/// <summary>A discount code (Flat amount or Percentage), with optional caps, window and usage limits.</summary>
public class Coupon : ITenantScoped
{
    public long CouponId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DiscountType { get; set; } = "Flat"; // Flat | Percentage
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }     // cap for percentage coupons
    public decimal? MinOrderAmount { get; set; }
    public int? UsageLimit { get; set; }                // total redemptions allowed
    public int? PerUserLimit { get; set; }
    public int UsedCount { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Records a single redemption of a coupon on an order (for per-user limits + reporting).</summary>
public class CouponUsage
{
    public long CouponUsageId { get; set; }
    public long CouponId { get; set; }
    public long UserId { get; set; }
    public long OrderId { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}
