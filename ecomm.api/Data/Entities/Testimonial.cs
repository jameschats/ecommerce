namespace ecomm.api.Data.Entities;

/// <summary>A customer quote shown in the home page's testimonials section. Admin-managed —
/// independent of the real product Reviews, which have no "feature on homepage" concept.</summary>
public class Testimonial
{
    public long TestimonialId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Name { get; set; } = "";
    public string? RoleOrCompany { get; set; }
    public string Quote { get; set; } = "";
    public byte Rating { get; set; } = 5;
    public string? PhotoUrl { get; set; }
    public byte[]? PhotoData { get; set; }
    public string? PhotoContentType { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
