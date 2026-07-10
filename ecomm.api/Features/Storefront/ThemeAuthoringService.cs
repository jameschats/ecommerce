using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Cms.SectionTypes;
using Ganss.Xss;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Storefront;

public sealed record ThemeSectionAdminDto(
    long Id, string SectionType, string Kind, string? Title, string? Settings, string? Blocks,
    int DisplayOrder, bool IsVisible, DateTime? StartsAt, DateTime? EndsAt);

public sealed record ThemeTemplateSummaryDto(string TemplateKey, string Label, string Group, int SectionCount);

public sealed record AddThemeSectionRequest(string SectionType);
public sealed record SaveThemeSectionRequest(string? Title, string? Settings, string? Blocks, bool IsVisible, DateTime? StartsAt, DateTime? EndsAt);
public sealed record ReorderThemeSectionsRequest(List<long> OrderedSectionIds);

public interface IThemeAuthoringService
{
    Task<IReadOnlyList<ThemeTemplateSummaryDto>> ListTemplatesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ThemeSectionAdminDto>> GetSectionsAsync(string templateKey, CancellationToken ct = default);
    Task<ThemeSectionAdminDto> AddSectionAsync(string templateKey, string sectionType, CancellationToken ct = default);
    Task<ThemeSectionAdminDto> UpdateSectionAsync(long sectionId, SaveThemeSectionRequest req, CancellationToken ct = default);
    Task<ThemeSectionAdminDto> DuplicateSectionAsync(long sectionId, CancellationToken ct = default);
    Task DeleteSectionAsync(long sectionId, CancellationToken ct = default);
    Task ReorderSectionsAsync(string templateKey, List<long> orderedIds, CancellationToken ct = default);
}

/// <summary>
/// Merchant authoring of the theme engine (S4): CRUD + reorder of the sections that compose each
/// page-type template (and the header/footer/announcement zones) of the tenant's theme. Mirrors the
/// CMS section authoring, but targets <see cref="ThemeTemplate"/>/<see cref="ThemeSection"/> and
/// enforces the section-type <c>Scope</c> (a section can only go on templates it's valid for).
/// </summary>
public sealed class ThemeAuthoringService(EcommerceDbContext db) : IThemeAuthoringService
{
    private long Tenant => db.CurrentTenantId;
    private readonly HtmlSanitizer _sanitizer = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly Dictionary<string, (string Label, string Group)> TemplateMeta = new(StringComparer.OrdinalIgnoreCase)
    {
        ["announcement"] = ("Announcement bar", "Header"),
        ["header"] = ("Header", "Header"),
        ["index"] = ("Home", "Templates"),
        ["product"] = ("Product", "Templates"),
        ["collection"] = ("Collection", "Templates"),
        ["list-collections"] = ("Collections list", "Templates"),
        ["cart"] = ("Cart", "Templates"),
        ["search"] = ("Search", "Templates"),
        ["404"] = ("404 not found", "Templates"),
        ["password"] = ("Password", "Templates"),
        ["account"] = ("Account", "Templates"),
        ["footer"] = ("Footer", "Footer"),
    };

    public async Task<IReadOnlyList<ThemeTemplateSummaryDto>> ListTemplatesAsync(CancellationToken ct = default)
    {
        var theme = await ResolveThemeAsync(ct);
        var counts = await db.ThemeSections
            .Where(s => s.Template!.ThemeId == theme.ThemeId)
            .GroupBy(s => s.Template!.TemplateKey)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return SectionTypeRegistry.TemplateKeys.Select(k =>
        {
            var meta = TemplateMeta.TryGetValue(k, out var m) ? m : (Label: k, Group: "Templates");
            return new ThemeTemplateSummaryDto(k, meta.Label, meta.Group, counts.GetValueOrDefault(k, 0));
        }).ToList();
    }

    public async Task<IReadOnlyList<ThemeSectionAdminDto>> GetSectionsAsync(string templateKey, CancellationToken ct = default)
    {
        var key = Normalize(templateKey);
        var theme = await ResolveThemeAsync(ct);
        var rows = await db.ThemeSections
            .Where(s => s.Template!.ThemeId == theme.ThemeId && s.Template.TemplateKey == key)
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new { s.ThemeSectionId, s.SectionType, s.Title, s.Settings, s.Blocks, s.DisplayOrder, s.IsVisible, s.StartsAt, s.EndsAt })
            .ToListAsync(ct);
        return rows.Select(s => new ThemeSectionAdminDto(
            s.ThemeSectionId, s.SectionType, KindOf(s.SectionType), s.Title, s.Settings, s.Blocks,
            s.DisplayOrder, s.IsVisible, s.StartsAt, s.EndsAt)).ToList();
    }

    public async Task<ThemeSectionAdminDto> AddSectionAsync(string templateKey, string sectionType, CancellationToken ct = default)
    {
        var key = Normalize(templateKey);
        var schema = SectionTypeRegistry.Get(sectionType)
            ?? throw new AppException("Unknown section type.", StatusCodes.Status400BadRequest);
        if (!SectionTypeRegistry.IsValidOnTemplate(sectionType, key))
            throw new AppException($"'{schema.Label}' can't be placed on this template.", StatusCodes.Status400BadRequest);

        var template = await EnsureTemplateAsync(key, ct);
        var maxOrder = await db.ThemeSections.Where(s => s.ThemeTemplateId == template.ThemeTemplateId)
            .Select(s => (int?)s.DisplayOrder).MaxAsync(ct) ?? 0;

        var section = new ThemeSection
        {
            ThemeTemplateId = template.ThemeTemplateId, SectionType = schema.Key, Title = schema.Label,
            Settings = DefaultSettings(schema), Blocks = "[]",
            DisplayOrder = maxOrder + 1, IsVisible = true, CreatedAt = DateTime.UtcNow,
        };
        db.ThemeSections.Add(section);   // TenantId auto-stamped
        await db.SaveChangesAsync(ct);
        return ToDto(section);
    }

    public async Task<ThemeSectionAdminDto> UpdateSectionAsync(long sectionId, SaveThemeSectionRequest req, CancellationToken ct = default)
    {
        var section = await db.ThemeSections.FirstOrDefaultAsync(s => s.ThemeSectionId == sectionId, ct) ?? throw NotFound();
        if (req.Title is not null) section.Title = req.Title;
        section.Settings = Sanitize(section.SectionType, req.Settings);
        section.Blocks = req.Blocks;
        section.IsVisible = req.IsVisible;
        section.StartsAt = req.StartsAt;
        section.EndsAt = req.EndsAt;
        section.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(section);
    }

    public async Task<ThemeSectionAdminDto> DuplicateSectionAsync(long sectionId, CancellationToken ct = default)
    {
        var s = await db.ThemeSections.FirstOrDefaultAsync(x => x.ThemeSectionId == sectionId, ct) ?? throw NotFound();
        var maxOrder = await db.ThemeSections.Where(x => x.ThemeTemplateId == s.ThemeTemplateId)
            .Select(x => (int?)x.DisplayOrder).MaxAsync(ct) ?? 0;
        var copy = new ThemeSection
        {
            ThemeTemplateId = s.ThemeTemplateId, SectionType = s.SectionType, Title = s.Title, Settings = s.Settings, Blocks = s.Blocks,
            DisplayOrder = maxOrder + 1, IsVisible = s.IsVisible, StartsAt = s.StartsAt, EndsAt = s.EndsAt, CreatedAt = DateTime.UtcNow,
        };
        db.ThemeSections.Add(copy);
        await db.SaveChangesAsync(ct);
        return ToDto(copy);
    }

    public async Task DeleteSectionAsync(long sectionId, CancellationToken ct = default)
    {
        var s = await db.ThemeSections.FirstOrDefaultAsync(x => x.ThemeSectionId == sectionId, ct) ?? throw NotFound();
        db.ThemeSections.Remove(s);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReorderSectionsAsync(string templateKey, List<long> orderedIds, CancellationToken ct = default)
    {
        var key = Normalize(templateKey);
        var theme = await ResolveThemeAsync(ct);
        var sections = await db.ThemeSections
            .Where(s => s.Template!.ThemeId == theme.ThemeId && s.Template.TemplateKey == key).ToListAsync(ct);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var s = sections.FirstOrDefault(x => x.ThemeSectionId == orderedIds[i]);
            if (s is not null) { s.DisplayOrder = i + 1; s.UpdatedAt = DateTime.UtcNow; }
        }
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers ----
    private async Task<Data.Entities.Theme> ResolveThemeAsync(CancellationToken ct)
    {
        var theme = await db.Themes.Where(t => t.TenantId == Tenant)
            .OrderByDescending(t => t.Status == "Published").ThenByDescending(t => t.IsActive).ThenBy(t => t.ThemeId)
            .FirstOrDefaultAsync(ct);
        if (theme is null)
        {
            theme = new Data.Entities.Theme { Name = "Default", IsActive = true, Status = "Published", CreatedAt = DateTime.UtcNow };
            db.Themes.Add(theme);
            await db.SaveChangesAsync(ct);
        }
        return theme;
    }

    private async Task<ThemeTemplate> EnsureTemplateAsync(string key, CancellationToken ct)
    {
        var theme = await ResolveThemeAsync(ct);
        var template = await db.ThemeTemplates.FirstOrDefaultAsync(t => t.ThemeId == theme.ThemeId && t.TemplateKey == key, ct);
        if (template is null)
        {
            template = new ThemeTemplate { ThemeId = theme.ThemeId, TemplateKey = key, Name = "Default", CreatedAt = DateTime.UtcNow };
            db.ThemeTemplates.Add(template);
            await db.SaveChangesAsync(ct);
        }
        return template;
    }

    private static string Normalize(string key)
    {
        var k = (key ?? "").Trim().ToLowerInvariant();
        if (!SectionTypeRegistry.IsValidTemplateKey(k)) throw new AppException("Unknown template.", StatusCodes.Status400BadRequest);
        return k;
    }

    private string? Sanitize(string sectionType, string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson)) return settingsJson;
        var richKeys = SectionTypeRegistry.RichTextSettingKeys(sectionType).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (richKeys.Count == 0) return settingsJson;
        if (JsonNode.Parse(settingsJson) is not JsonObject obj) return settingsJson;
        foreach (var k in richKeys)
            if (obj[k] is JsonValue v && v.TryGetValue<string>(out var html))
                obj[k] = _sanitizer.Sanitize(html);
        return obj.ToJsonString(JsonOpts);
    }

    private static string DefaultSettings(SectionTypeSchema schema)
    {
        var obj = new JsonObject();
        foreach (var f in schema.Settings.Where(f => f.Default is not null))
            obj[f.Key] = ToNode(f.Default!);
        return obj.ToJsonString(JsonOpts);
    }

    /// <summary>Typed JSON node from a boxed default — typed values serialize without a TypeInfoResolver.</summary>
    private static JsonNode? ToNode(object v) => v switch
    {
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d => JsonValue.Create(d),
        string s => JsonValue.Create(s),
        _ => JsonValue.Create(v.ToString()),
    };

    private static string KindOf(string sectionType) => SectionTypeRegistry.Get(sectionType)?.Kind ?? "static";
    private static ThemeSectionAdminDto ToDto(ThemeSection s) => new(
        s.ThemeSectionId, s.SectionType, KindOf(s.SectionType), s.Title, s.Settings, s.Blocks, s.DisplayOrder, s.IsVisible, s.StartsAt, s.EndsAt);
    private static AppException NotFound() => new("Not found.", StatusCodes.Status404NotFound);
}
