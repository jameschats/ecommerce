namespace ecomm.api.Data.Entities;

public class OrderStatusHistory
{
    public long OrderStatusHistoryId { get; set; }
    public long OrderId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public long? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
