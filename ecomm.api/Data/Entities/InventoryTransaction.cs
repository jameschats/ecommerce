namespace ecomm.api.Data.Entities;

public class InventoryTransaction
{
    public long InventoryTransactionId { get; set; }
    public long InventoryId { get; set; }
    public long ProductId { get; set; }
    public int ChangeQty { get; set; }                 // signed: +receipt / -sale
    public int? BalanceAfter { get; set; }
    public string TransactionType { get; set; } = string.Empty;  // Purchase|Sale|Reservation|Release|Adjustment|Return
    public string? ReferenceType { get; set; }          // Order | ImportJob | Manual
    public long? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
