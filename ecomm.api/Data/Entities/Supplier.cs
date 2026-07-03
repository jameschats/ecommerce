namespace ecomm.api.Data.Entities;

/// <summary>A supplier/vendor a product can be sourced from (migration 021).</summary>
public class Supplier : ITenantScoped
{
    public long SupplierId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Gstin { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string Country { get; set; } = "India";
    public string? PaymentTerms { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Links a product (optionally a variant) to a supplier with a per-supplier cost (migration 021).</summary>
public class ProductSupplier
{
    public long ProductSupplierId { get; set; }
    public long TenantId { get; set; } = 1;
    public long ProductId { get; set; }
    public long? ProductVariantId { get; set; }
    public long SupplierId { get; set; }
    public string? SupplierSku { get; set; }
    public decimal? CostPrice { get; set; }
    public string Currency { get; set; } = "INR";
    public int? LeadTimeDays { get; set; }
    public int? MinOrderQty { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
