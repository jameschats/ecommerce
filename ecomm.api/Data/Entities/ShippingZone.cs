namespace ecomm.api.Data.Entities;

public class ShippingZone : ITenantScoped
{
    public long ShippingZoneId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public long? ShippingMethodId { get; set; }
    public string? PincodeStart { get; set; }
    public string? PincodeEnd { get; set; }
    public decimal Rate { get; set; }
    public bool IsServiceable { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
