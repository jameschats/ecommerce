namespace ecomm.api.Data.Entities;

public class ImportJob : ITenantScoped
{
    public long ImportJobId { get; set; }
    public long TenantId { get; set; } = 1;
    public string JobType { get; set; } = string.Empty;   // Products | Inventory | ...
    public string? FileName { get; set; }
    public string? FileUrl { get; set; }
    public string Status { get; set; } = "Pending";        // Pending|Processing|Completed|Failed|PartiallyCompleted
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<ImportJobItem> Items { get; set; } = new List<ImportJobItem>();
}
