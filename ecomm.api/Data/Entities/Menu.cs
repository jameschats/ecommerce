namespace ecomm.api.Data.Entities;

/// <summary>A storefront navigation menu (main / footer / account). Items are JSON: label + url + children.</summary>
public class Menu : ITenantScoped
{
    public long MenuId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Handle { get; set; } = string.Empty;   // main-menu | footer | account
    public string Title { get; set; } = string.Empty;
    public string? ItemsJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>A from→to URL redirect (e.g. an old product URL to a new one).</summary>
public class UrlRedirect : ITenantScoped
{
    public long UrlRedirectId { get; set; }
    public long TenantId { get; set; } = 1;
    public string FromPath { get; set; } = string.Empty;
    public string ToPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
