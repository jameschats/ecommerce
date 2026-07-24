namespace ecomm.api.Data.Entities;

/// <summary>
/// Minimum order value for a delivery state. Overrides the global
/// <c>QuickOrder.MinOrderAmount</c> setting; an empty table means the global value
/// applies everywhere. See documents/stages-v2/design.md §6.
/// </summary>
public class StateMinOrderAmount
{
    public long StateMinOrderAmountId { get; set; }
    public long TenantId { get; set; } = 1;
    public string StateName { get; set; } = string.Empty;
    public decimal MinOrderAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
