using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Storefront;

public sealed record ThemeSummaryDto(
    long ThemeId, string Name, string Status, string? Source, bool IsPublished, string? PreviewToken, DateTime CreatedAt);
public sealed record CreateThemeRequest(string Name);
public sealed record RenameThemeRequest(string Name);
public sealed record DuplicateThemeRequest(string? Name);
public sealed record InstallThemeRequest(string Key);

public interface IThemeLibraryService
{
    Task<IReadOnlyList<ThemeSummaryDto>> ListAsync(CancellationToken ct = default);
    Task<ThemeSummaryDto> GetAsync(long themeId, CancellationToken ct = default);
    Task<ThemeSummaryDto> CreateAsync(string name, CancellationToken ct = default);
    Task<ThemeSummaryDto> DuplicateAsync(long themeId, string? name, CancellationToken ct = default);
    Task<ThemeSummaryDto> RenameAsync(long themeId, string name, CancellationToken ct = default);
    Task PublishAsync(long themeId, CancellationToken ct = default);
    Task DeleteAsync(long themeId, CancellationToken ct = default);
    IReadOnlyList<PrebuiltThemeSummary> ListPrebuilt();
    Task<ThemeSummaryDto> InstallPrebuiltAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// The tenant's theme library (S5): many themes, exactly one Published (the live storefront), the rest
/// Draft. Supports create / duplicate (deep copy) / rename / delete / atomic publish. Each theme carries
/// a PreviewToken so a Draft can be previewed on the storefront before it goes live.
/// </summary>
public sealed class ThemeLibraryService(EcommerceDbContext db) : IThemeLibraryService
{
    private long Tenant => db.CurrentTenantId;

    public async Task<IReadOnlyList<ThemeSummaryDto>> ListAsync(CancellationToken ct = default) =>
        await db.Themes.AsNoTracking()
            .OrderByDescending(t => t.Status == "Published").ThenByDescending(t => t.ThemeId)
            .Select(t => new ThemeSummaryDto(t.ThemeId, t.Name, t.Status, t.Source, t.Status == "Published", t.PreviewToken, t.CreatedAt))
            .ToListAsync(ct);

    public async Task<ThemeSummaryDto> GetAsync(long themeId, CancellationToken ct = default) => ToDto(await FindAsync(themeId, ct));

    public async Task<ThemeSummaryDto> CreateAsync(string name, CancellationToken ct = default)
    {
        var theme = new Data.Entities.Theme
        {
            Name = Clean(name, "New theme"), Status = "Draft", IsActive = false,
            PreviewToken = NewToken(), CreatedAt = DateTime.UtcNow,
        };
        db.Themes.Add(theme);   // TenantId auto-stamped
        await db.SaveChangesAsync(ct);
        return ToDto(theme);
    }

    public async Task<ThemeSummaryDto> DuplicateAsync(long themeId, string? name, CancellationToken ct = default)
    {
        var src = await FindAsync(themeId, ct);
        var copy = new Data.Entities.Theme
        {
            Name = Clean(name, $"{src.Name} copy"), Status = "Draft", IsActive = false,
            Source = src.Source, PreviewToken = NewToken(), CreatedAt = DateTime.UtcNow,
        };
        db.Themes.Add(copy);
        await db.SaveChangesAsync(ct);   // need copy.ThemeId

        // Copy global settings.
        var settings = await db.ThemeSettings.AsNoTracking().Where(s => s.ThemeId == src.ThemeId).ToListAsync(ct);
        foreach (var s in settings)
            db.ThemeSettings.Add(new ThemeSetting { ThemeId = copy.ThemeId, SettingKey = s.SettingKey, SettingValue = s.SettingValue, CreatedAt = DateTime.UtcNow });

        // Copy each template + its sections.
        var templates = await db.ThemeTemplates.AsNoTracking().Where(t => t.ThemeId == src.ThemeId).ToListAsync(ct);
        foreach (var tpl in templates)
        {
            var newTpl = new ThemeTemplate { ThemeId = copy.ThemeId, TemplateKey = tpl.TemplateKey, Name = tpl.Name, CreatedAt = DateTime.UtcNow };
            db.ThemeTemplates.Add(newTpl);
            await db.SaveChangesAsync(ct);   // need newTpl.ThemeTemplateId

            var sections = await db.ThemeSections.AsNoTracking().Where(x => x.ThemeTemplateId == tpl.ThemeTemplateId).ToListAsync(ct);
            foreach (var sec in sections)
                db.ThemeSections.Add(new ThemeSection
                {
                    ThemeTemplateId = newTpl.ThemeTemplateId, SectionType = sec.SectionType, Title = sec.Title,
                    Settings = sec.Settings, Blocks = sec.Blocks, DisplayOrder = sec.DisplayOrder, IsVisible = sec.IsVisible,
                    StartsAt = sec.StartsAt, EndsAt = sec.EndsAt, CreatedAt = DateTime.UtcNow,
                });
        }
        await db.SaveChangesAsync(ct);
        return ToDto(copy);
    }

    public async Task<ThemeSummaryDto> RenameAsync(long themeId, string name, CancellationToken ct = default)
    {
        var theme = await FindAsync(themeId, ct);
        theme.Name = Clean(name, theme.Name);
        theme.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(theme);
    }

    /// <summary>Atomic swap: the target becomes the one Published (+active) theme; every other goes Draft.</summary>
    public async Task PublishAsync(long themeId, CancellationToken ct = default)
    {
        var target = await FindAsync(themeId, ct);
        var themes = await db.Themes.Where(t => t.TenantId == Tenant).ToListAsync(ct);
        foreach (var t in themes)
        {
            var isTarget = t.ThemeId == target.ThemeId;
            t.Status = isTarget ? "Published" : "Draft";
            t.IsActive = isTarget;
            t.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);   // single transaction → exactly one Published
    }

    public async Task DeleteAsync(long themeId, CancellationToken ct = default)
    {
        var theme = await FindAsync(themeId, ct);
        if (theme.Status == "Published")
            throw new AppException("Publish another theme before deleting the live one.", StatusCodes.Status400BadRequest);

        var templateIds = await db.ThemeTemplates.Where(t => t.ThemeId == themeId).Select(t => t.ThemeTemplateId).ToListAsync(ct);
        db.ThemeSections.RemoveRange(db.ThemeSections.Where(s => templateIds.Contains(s.ThemeTemplateId)));
        db.ThemeTemplates.RemoveRange(db.ThemeTemplates.Where(t => t.ThemeId == themeId));
        db.ThemeSettings.RemoveRange(db.ThemeSettings.Where(s => s.ThemeId == themeId));
        db.Themes.Remove(theme);
        await db.SaveChangesAsync(ct);
    }

    public IReadOnlyList<PrebuiltThemeSummary> ListPrebuilt() => PrebuiltThemeRegistry.Summaries;

    /// <summary>Install a free prebuilt theme into the library as a Draft (settings + templates + sections).</summary>
    public async Task<ThemeSummaryDto> InstallPrebuiltAsync(string key, CancellationToken ct = default)
    {
        var bundle = PrebuiltThemeRegistry.Get(key)
            ?? throw new AppException("Unknown theme.", StatusCodes.Status404NotFound);

        var theme = new Data.Entities.Theme
        {
            Name = bundle.Name, Status = "Draft", IsActive = false,
            Source = bundle.Key, PreviewToken = NewToken(), CreatedAt = DateTime.UtcNow,
        };
        db.Themes.Add(theme);
        await db.SaveChangesAsync(ct);   // need theme.ThemeId

        foreach (var (k, v) in bundle.Settings)
            db.ThemeSettings.Add(new ThemeSetting { ThemeId = theme.ThemeId, SettingKey = k, SettingValue = v, CreatedAt = DateTime.UtcNow });

        foreach (var tpl in bundle.Templates)
        {
            var template = new ThemeTemplate { ThemeId = theme.ThemeId, TemplateKey = tpl.TemplateKey, Name = "Default", CreatedAt = DateTime.UtcNow };
            db.ThemeTemplates.Add(template);
            await db.SaveChangesAsync(ct);   // need template.ThemeTemplateId

            var order = 1;
            foreach (var sec in tpl.Sections)
                db.ThemeSections.Add(new ThemeSection
                {
                    ThemeTemplateId = template.ThemeTemplateId, SectionType = sec.Type, Title = sec.Title,
                    Settings = sec.Settings, Blocks = sec.Blocks, DisplayOrder = order++, IsVisible = true, CreatedAt = DateTime.UtcNow,
                });
        }
        await db.SaveChangesAsync(ct);
        return ToDto(theme);
    }

    // ---- helpers ----
    private async Task<Data.Entities.Theme> FindAsync(long themeId, CancellationToken ct) =>
        await db.Themes.FirstOrDefaultAsync(t => t.ThemeId == themeId, ct)
            ?? throw new AppException("Theme not found.", StatusCodes.Status404NotFound);

    private static string NewToken() => Guid.NewGuid().ToString("N");
    private static string Clean(string? name, string fallback) => string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
    private static ThemeSummaryDto ToDto(Data.Entities.Theme t) => new(t.ThemeId, t.Name, t.Status, t.Source, t.Status == "Published", t.PreviewToken, t.CreatedAt);
}
