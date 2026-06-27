namespace ecomm.api.Data.Entities;

public class SearchLog
{
    public long SearchLogId { get; set; }
    public long TenantId { get; set; } = 1;
    public long? UserId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public int ResultsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
