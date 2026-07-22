namespace ecomm.api.Data.Entities;

public class Order : ITenantScoped
{
    public long OrderId { get; set; }
    public long TenantId { get; set; } = 1;
    public long? SellerId { get; set; }
    public long UserId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";   // Pending|Paid|Packed|Shipped|Delivered|Cancelled|Returned
    public long? CouponId { get; set; }
    public long? BillingAddressId { get; set; }
    public long? ShippingAddressId { get; set; }
    public long? ShippingMethodId { get; set; }
    public string Currency { get; set; } = "INR";
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Placed by the merchant's "try a test order" walkthrough, not a real customer.
    /// Kept (never deleted) so invoice numbering stays contiguous; excluded from all analytics.
    /// </summary>
    public bool IsTest { get; set; }
    public DateTime? PlacedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
