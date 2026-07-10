using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Cms.SectionTypes;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Storefront;

public sealed record ThemeSectionDto(
    long Id, string SectionType, string Kind, string? Title, string? Settings, string? Blocks, int DisplayOrder, bool IsVisible);

/// <summary>Published theme: global settings + the shared header/footer/announcement zones.</summary>
public sealed record ThemeBundleDto(
    long ThemeId, string Status, Dictionary<string, string> Settings,
    IReadOnlyList<ThemeSectionDto> Header, IReadOnlyList<ThemeSectionDto> Footer, IReadOnlyList<ThemeSectionDto> Announcement);

/// <summary>
/// A page-type template = its ordered section list (layout is type-level; entity data comes from catalog/cart
/// endpoints). <c>Authored</c> is true when the sections come from the theme's own template (vs the transitional
/// Home-page fallback) — the home only switches to theme-driven rendering when its index is actually authored.
/// </summary>
public sealed record ThemeTemplateDto(string TemplateKey, bool Authored, IReadOnlyList<ThemeSectionDto> Sections);

public interface IStorefrontThemeService
{
    Task<ThemeBundleDto> GetPublishedBundleAsync(string? previewToken = null, CancellationToken ct = default);
    Task<ThemeTemplateDto> GetTemplateAsync(string templateKey, string? previewToken = null, CancellationToken ct = default);
    /// <summary>Idempotently copies the current Home page's sections into the published theme's <c>index</c> template.</summary>
    Task<int> BackfillIndexFromHomeAsync(CancellationToken ct = default);
}

/// <summary>
/// Read model for the storefront theme engine (S1). Reads the published theme's global settings
/// (from the existing ThemeSettings key/value store) and its per-page-type templates. Until a
/// theme's <c>index</c> template is populated, it transparently falls back to the current Home
/// page sections — so the storefront keeps rendering unchanged while the engine is introduced.
/// </summary>
public sealed class StorefrontThemeService(EcommerceDbContext db) : IStorefrontThemeService
{
    private long Tenant => db.CurrentTenantId;

    public async Task<ThemeBundleDto> GetPublishedBundleAsync(string? previewToken = null, CancellationToken ct = default)
    {
        var theme = await ThemeForRequestAsync(previewToken, ct);
        if (theme is null) return new ThemeBundleDto(0, "None", new(), [], [], []);

        var settings = await db.ThemeSettings.AsNoTracking()
            .Where(s => s.ThemeId == theme.ThemeId)
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? string.Empty, ct);

        // Default the storefront name to the store's own name (so a fresh store shows its brand,
        // not a hardcoded fallback) unless the merchant set a StoreName in theme settings.
        if (string.IsNullOrWhiteSpace(settings.GetValueOrDefault("StoreName")))
        {
            var name = await db.Tenants.AsNoTracking()
                .Where(t => t.TenantId == Tenant).Select(t => t.DisplayName ?? t.Name).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(name)) settings["StoreName"] = name!;
        }

        return new ThemeBundleDto(
            theme.ThemeId, theme.Status, settings,
            await GroupSectionsAsync(theme.ThemeId, "header", ct),
            await GroupSectionsAsync(theme.ThemeId, "footer", ct),
            await GroupSectionsAsync(theme.ThemeId, "announcement", ct));
    }

    public async Task<ThemeTemplateDto> GetTemplateAsync(string templateKey, string? previewToken = null, CancellationToken ct = default)
    {
        var key = templateKey.ToLowerInvariant();
        if (!SectionTypeRegistry.IsValidTemplateKey(key)) return new ThemeTemplateDto(key, false, []);

        var theme = await ThemeForRequestAsync(previewToken, ct);
        var sections = theme is null ? [] : await TemplateSectionsAsync(theme.ThemeId, key, ct);
        var authored = sections.Count > 0;

        // Transitional fallback: an un-populated `index` renders today's Home layout (read-only, no writes).
        // This is NOT "authored" — the home keeps its legacy rendering until a real theme index exists.
        if (sections.Count == 0 && key == "index")
            sections = await HomeAsThemeSectionsAsync(ct);

        return new ThemeTemplateDto(key, authored, sections);
    }

    public async Task<int> BackfillIndexFromHomeAsync(CancellationToken ct = default)
    {
        var theme = await PublishedThemeAsync(ct);
        if (theme is null) return 0;

        var template = await db.ThemeTemplates.FirstOrDefaultAsync(t => t.ThemeId == theme.ThemeId && t.TemplateKey == "index", ct);
        if (template is null)
        {
            template = new ThemeTemplate { ThemeId = theme.ThemeId, TemplateKey = "index", Name = "Default", CreatedAt = DateTime.UtcNow };
            db.ThemeTemplates.Add(template);
            await db.SaveChangesAsync(ct);   // need the id for sections
        }

        // Idempotent: only seed when the template is still empty.
        if (await db.ThemeSections.AnyAsync(s => s.ThemeTemplateId == template.ThemeTemplateId, ct)) return 0;

        var home = await db.Pages.FirstOrDefaultAsync(p => p.Slug == "home", ct);
        if (home is null) return 0;
        var homeSections = await db.PageSections.AsNoTracking()
            .Where(s => s.PageId == home.PageId).OrderBy(s => s.DisplayOrder).ToListAsync(ct);
        if (homeSections.Count == 0) return 0;

        foreach (var s in homeSections)
            db.ThemeSections.Add(new ThemeSection
            {
                ThemeTemplateId = template.ThemeTemplateId, SectionType = s.SectionType, Title = s.Title,
                Settings = s.Settings, Blocks = s.Blocks, DisplayOrder = s.DisplayOrder, IsVisible = s.IsVisible,
                StartsAt = s.StartsAt, EndsAt = s.EndsAt, CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync(ct);
        return homeSections.Count;
    }

    // ---- helpers ----
    private Task<Data.Entities.Theme?> PublishedThemeAsync(CancellationToken ct) =>
        db.Themes.AsNoTracking().Where(t => t.TenantId == Tenant)
            .OrderByDescending(t => t.Status == "Published").ThenByDescending(t => t.IsActive).ThenBy(t => t.ThemeId)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// The theme to render: a Draft matched by <paramref name="previewToken"/> (admin preview of a theme
    /// before it's live), otherwise the Published theme. An unknown token falls back to Published.
    /// </summary>
    private async Task<Data.Entities.Theme?> ThemeForRequestAsync(string? previewToken, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(previewToken))
        {
            var preview = await db.Themes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantId == Tenant && t.PreviewToken == previewToken, ct);
            if (preview is not null) return preview;
        }
        return await PublishedThemeAsync(ct);
    }

    private async Task<IReadOnlyList<ThemeSectionDto>> GroupSectionsAsync(long themeId, string groupKey, CancellationToken ct) =>
        await TemplateSectionsAsync(themeId, groupKey, ct);

    private async Task<List<ThemeSectionDto>> TemplateSectionsAsync(long themeId, string templateKey, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await db.ThemeSections.AsNoTracking()
            .Where(s => s.Template!.ThemeId == themeId && s.Template.TemplateKey == templateKey
                && s.IsVisible && (s.StartsAt == null || s.StartsAt <= now) && (s.EndsAt == null || s.EndsAt >= now))
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new { s.ThemeSectionId, s.SectionType, s.Title, s.Settings, s.Blocks, s.DisplayOrder, s.IsVisible })
            .ToListAsync(ct);
        return rows.Select(s => new ThemeSectionDto(s.ThemeSectionId, s.SectionType, KindOf(s.SectionType), s.Title, s.Settings, s.Blocks, s.DisplayOrder, s.IsVisible)).ToList();
    }

    private async Task<List<ThemeSectionDto>> HomeAsThemeSectionsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var home = await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == "home", ct);
        if (home is null) return [];
        var rows = await db.PageSections.AsNoTracking()
            .Where(s => s.PageId == home.PageId && s.IsVisible
                && (s.StartsAt == null || s.StartsAt <= now) && (s.EndsAt == null || s.EndsAt >= now))
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new { s.PageSectionId, s.SectionType, s.Title, s.Settings, s.Blocks, s.DisplayOrder, s.IsVisible })
            .ToListAsync(ct);
        return rows.Select(s => new ThemeSectionDto(s.PageSectionId, s.SectionType, KindOf(s.SectionType), s.Title, s.Settings, s.Blocks, s.DisplayOrder, s.IsVisible)).ToList();
    }

    private static string KindOf(string sectionType) => SectionTypeRegistry.Get(sectionType)?.Kind ?? "static";
}
