namespace ecomm.api.Data.Entities;

/// <summary>A dispatch of an order: courier + tracking number + delivery lifecycle.</summary>
public class Shipment : ITenantScoped
{
    public long ShipmentId { get; set; }
    public long TenantId { get; set; } = 1;
    public long OrderId { get; set; }
    public long? ShippingMethodId { get; set; }
    public string? Courier { get; set; }
    public string? TrackingNumber { get; set; }
    public string Status { get; set; } = "Pending"; // Pending|Packed|Shipped|InTransit|Delivered|Returned
    public DateTime? EstimatedDeliveryDate { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
