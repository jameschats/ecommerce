namespace ecomm.api.Data.Entities;

public class Theme : ITenantScoped
{
    public long ThemeId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Status { get; set; } = "Draft";   // Draft | Published (exactly one Published per tenant)
    public string? Source { get; set; }              // prebuilt bundle key this theme was installed from
    public string? PreviewToken { get; set; }        // token that lets an admin preview a Draft on the storefront
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ThemeSetting> Settings { get; set; } = new List<ThemeSetting>();
}

/// <summary>One page-type layout within a theme (index/product/collection/…, plus the header/footer/announcement groups).</summary>
public class ThemeTemplate : ITenantScoped
{
    public long ThemeTemplateId { get; set; }
    public long TenantId { get; set; } = 1;
    public long ThemeId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string Name { get; set; } = "Default";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Theme? Theme { get; set; }
    public ICollection<ThemeSection> Sections { get; set; } = new List<ThemeSection>();
}

/// <summary>An ordered section composing a <see cref="ThemeTemplate"/> (PageSection generalised to a template).</summary>
public class ThemeSection : ITenantScoped
{
    public long ThemeSectionId { get; set; }
    public long TenantId { get; set; } = 1;
    public long ThemeTemplateId { get; set; }
    public string SectionType { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Settings { get; set; }   // JSON — per-section settings
    public string? Blocks { get; set; }      // JSON — ordered child blocks
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ThemeTemplate? Template { get; set; }
}
