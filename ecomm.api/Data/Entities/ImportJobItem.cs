namespace ecomm.api.Data.Entities;

public class ImportJobItem
{
    public long ImportJobItemId { get; set; }
    public long ImportJobId { get; set; }
    public int RowNumber { get; set; }
    public string Status { get; set; } = string.Empty;   // Success | Failed
    public string? ErrorMessage { get; set; }
    public string? RawData { get; set; }
    public DateTime CreatedAt { get; set; }

    public ImportJob? Job { get; set; }
}
