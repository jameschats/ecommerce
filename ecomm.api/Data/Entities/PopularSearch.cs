namespace ecomm.api.Data.Entities;

public class PopularSearch
{
    public long PopularSearchId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Term { get; set; } = string.Empty;
    public long SearchCount { get; set; }
    public DateTime? LastSearchedAt { get; set; }
}
