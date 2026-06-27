namespace ecomm.api.Data.Entities;

public class TaxRate
{
    public long TaxRateId { get; set; }
    public long TenantId { get; set; } = 1;
    public string? HsnCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal CgstRate { get; set; }
    public decimal SgstRate { get; set; }
    public decimal IgstRate { get; set; }
    public decimal TotalRate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
