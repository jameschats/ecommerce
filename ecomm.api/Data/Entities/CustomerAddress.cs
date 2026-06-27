namespace ecomm.api.Data.Entities;

public class CustomerAddress
{
    public long CustomerAddressId { get; set; }
    public long TenantId { get; set; } = 1;
    public long UserId { get; set; }
    public string? Label { get; set; }
    public string? RecipientName { get; set; }
    public string? Phone { get; set; }
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
    public string Country { get; set; } = "India";
    public string AddressType { get; set; } = "Both";   // Billing | Shipping | Both
    public bool IsDefault { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
