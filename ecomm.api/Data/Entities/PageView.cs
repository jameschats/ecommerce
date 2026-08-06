namespace ecomm.api.Data.Entities;

/// <summary>One page view from the first-party traffic tracker (see Features/Analytics).</summary>
public class PageView
{
    public long PageViewId { get; set; }
    public long TenantId { get; set; } = 1;
    public string VisitorId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Referrer { get; set; }
    public string DeviceType { get; set; } = "Desktop";
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public DateTime CreatedAt { get; set; }
}
