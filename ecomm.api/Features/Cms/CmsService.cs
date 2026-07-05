using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Cms.SectionTypes;
using Ganss.Xss;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Cms;

public sealed record SectionDto(
    long PageSectionId, long PageId, string SectionType, string? Title, string? Settings, string? Blocks,
    int DisplayOrder, bool IsVisible, DateTime? StartsAt, DateTime? EndsAt);

public sealed record PageDto(long PageId, string Title, string Slug, string Type, bool IsPublished, string? MetaTitle, string? MetaDescription);
public sealed record PageDetailDto(PageDto Page, List<SectionDto> Sections);

public sealed record SavePageRequest(string Title, string Slug, bool IsPublished, string? MetaTitle, string? MetaDescription);
public sealed record AddSectionRequest(long PageId, string SectionType);
public sealed record SaveSectionRequest(string? Title, string? Settings, string? Blocks, bool IsVisible, DateTime? StartsAt, DateTime? EndsAt);
public sealed record ReorderRequest(List<long> OrderedSectionIds);
public sealed record ApplyPresetRequest(string? PresetKey);

// Back-compat with the current storefront home endpoint.
public sealed record UpdateSectionItem(long PageSectionId, int DisplayOrder, bool IsVisible, string? Title, DateTime? StartsAt, DateTime? EndsAt);
public sealed record UpdateSectionsRequest(List<UpdateSectionItem> Sections);

public interface ICmsService
{
    // Public storefront
    Task<List<SectionDto>> GetHomeSectionsAsync(bool visibleOnly, CancellationToken ct = default);
    Task<PageDetailDto?> GetPageBySlugAsync(string slug, CancellationToken ct = default);
    // Admin — pages
    Task<List<PageDto>> ListPagesAsync(CancellationToken ct = default);
    Task<PageDetailDto?> GetPageAsync(long pageId, CancellationToken ct = default);
    Task<PageDto> CreatePageAsync(SavePageRequest req, CancellationToken ct = default);
    Task<PageDto> UpdatePageAsync(long pageId, SavePageRequest req, CancellationToken ct = default);
    Task DeletePageAsync(long pageId, CancellationToken ct = default);
    // Admin — sections
    Task<SectionDto> AddSectionAsync(AddSectionRequest req, CancellationToken ct = default);
    Task<SectionDto> UpdateSectionAsync(long sectionId, SaveSectionRequest req, CancellationToken ct = default);
    Task<SectionDto> DuplicateSectionAsync(long sectionId, CancellationToken ct = default);
    Task DeleteSectionAsync(long sectionId, CancellationToken ct = default);
    Task ReorderSectionsAsync(long pageId, List<long> orderedIds, CancellationToken ct = default);
    Task<List<SectionDto>> UpdateHomeSectionsAsync(List<UpdateSectionItem> items, CancellationToken ct = default);
    // Presets
    IReadOnlyList<PresetSummary> ListPresets();
    Task<PageDetailDto> ApplyPresetAsync(long pageId, string presetKey, CancellationToken ct = default);
}

public sealed class CmsService(EcommerceDbContext db) : ICmsService
{
    private readonly HtmlSanitizer _sanitizer = new();
    // Keep sanitized HTML readable in the stored JSON (don't \u-escape <,>,&). Safe: the value is
    // already HTML-sanitized and Angular re-sanitizes on render. Mirrors MySQL's JSON normalization.
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // ---- Public ----
    public async Task<List<SectionDto>> GetHomeSectionsAsync(bool visibleOnly, CancellationToken ct = default)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Slug == "home", ct);
        return page is null ? [] : await SectionsForPageAsync(page.PageId, visibleOnly, ct);
    }

    public async Task<PageDetailDto?> GetPageBySlugAsync(string slug, CancellationToken ct = default)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);
        return page is null ? null : new PageDetailDto(ToDto(page), await SectionsForPageAsync(page.PageId, visibleOnly: true, ct));
    }

    // ---- Admin: pages ----
    public async Task<List<PageDto>> ListPagesAsync(CancellationToken ct = default) =>
        await db.Pages.OrderBy(p => p.Type == "Home" ? 0 : 1).ThenBy(p => p.Title)
            .Select(p => new PageDto(p.PageId, p.Title, p.Slug, p.Type, p.IsPublished, p.MetaTitle, p.MetaDescription)).ToListAsync(ct);

    public async Task<PageDetailDto?> GetPageAsync(long pageId, CancellationToken ct = default)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.PageId == pageId, ct);
        return page is null ? null : new PageDetailDto(ToDto(page), await SectionsForPageAsync(pageId, visibleOnly: false, ct));
    }

    public async Task<PageDto> CreatePageAsync(SavePageRequest req, CancellationToken ct = default)
    {
        var slug = NormalizeSlug(req.Slug, req.Title);
        if (await db.Pages.AnyAsync(p => p.Slug == slug, ct))
            throw new AppException("A page with that address already exists.", StatusCodes.Status409Conflict);
        var page = new Page
        {
            Title = req.Title.Trim(), Slug = slug, Type = "Custom", IsPublished = req.IsPublished,
            MetaTitle = req.MetaTitle, MetaDescription = req.MetaDescription, CreatedAt = DateTime.UtcNow,
        };
        db.Pages.Add(page);
        await db.SaveChangesAsync(ct);
        return ToDto(page);
    }

    public async Task<PageDto> UpdatePageAsync(long pageId, SavePageRequest req, CancellationToken ct = default)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.PageId == pageId, ct) ?? throw NotFound();
        var slug = NormalizeSlug(req.Slug, req.Title);
        if (page.Type != "Home" && await db.Pages.AnyAsync(p => p.Slug == slug && p.PageId != pageId, ct))
            throw new AppException("A page with that address already exists.", StatusCodes.Status409Conflict);
        page.Title = req.Title.Trim();
        if (page.Type != "Home") page.Slug = slug;   // home slug is fixed
        page.IsPublished = req.IsPublished;
        page.MetaTitle = req.MetaTitle;
        page.MetaDescription = req.MetaDescription;
        page.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(page);
    }

    public async Task DeletePageAsync(long pageId, CancellationToken ct = default)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.PageId == pageId, ct) ?? throw NotFound();
        if (page.Type == "Home") throw new AppException("The home page can't be deleted.", StatusCodes.Status400BadRequest);
        var sections = await db.PageSections.Where(s => s.PageId == pageId).ToListAsync(ct);
        db.PageSections.RemoveRange(sections);
        db.Pages.Remove(page);
        await db.SaveChangesAsync(ct);
    }

    // ---- Admin: sections ----
    public async Task<SectionDto> AddSectionAsync(AddSectionRequest req, CancellationToken ct = default)
    {
        if (!SectionTypeRegistry.IsValidType(req.SectionType))
            throw new AppException("Unknown section type.", StatusCodes.Status400BadRequest);
        var page = await db.Pages.FirstOrDefaultAsync(p => p.PageId == req.PageId, ct) ?? throw NotFound();
        var maxOrder = await db.PageSections.Where(s => s.PageId == page.PageId).Select(s => (int?)s.DisplayOrder).MaxAsync(ct) ?? 0;
        var schema = SectionTypeRegistry.Get(req.SectionType)!;
        var section = new PageSection
        {
            PageId = page.PageId, SectionType = schema.Key, Title = schema.Label,
            Settings = DefaultSettings(schema), Blocks = "[]",
            DisplayOrder = maxOrder + 1, IsVisible = true, CreatedAt = DateTime.UtcNow,
        };
        db.PageSections.Add(section);   // TenantId auto-stamped
        await db.SaveChangesAsync(ct);
        return ToDto(section);
    }

    public async Task<SectionDto> UpdateSectionAsync(long sectionId, SaveSectionRequest req, CancellationToken ct = default)
    {
        var section = await db.PageSections.FirstOrDefaultAsync(s => s.PageSectionId == sectionId, ct) ?? throw NotFound();
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

    public async Task<SectionDto> DuplicateSectionAsync(long sectionId, CancellationToken ct = default)
    {
        var s = await db.PageSections.FirstOrDefaultAsync(x => x.PageSectionId == sectionId, ct) ?? throw NotFound();
        var maxOrder = await db.PageSections.Where(x => x.PageId == s.PageId).Select(x => (int?)x.DisplayOrder).MaxAsync(ct) ?? 0;
        var copy = new PageSection
        {
            PageId = s.PageId, SectionType = s.SectionType, Title = s.Title, Settings = s.Settings, Blocks = s.Blocks,
            DisplayOrder = maxOrder + 1, IsVisible = s.IsVisible, StartsAt = s.StartsAt, EndsAt = s.EndsAt, CreatedAt = DateTime.UtcNow,
        };
        db.PageSections.Add(copy);
        await db.SaveChangesAsync(ct);
        return ToDto(copy);
    }

    public async Task DeleteSectionAsync(long sectionId, CancellationToken ct = default)
    {
        var s = await db.PageSections.FirstOrDefaultAsync(x => x.PageSectionId == sectionId, ct) ?? throw NotFound();
        db.PageSections.Remove(s);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReorderSectionsAsync(long pageId, List<long> orderedIds, CancellationToken ct = default)
    {
        var sections = await db.PageSections.Where(s => s.PageId == pageId).ToListAsync(ct);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var s = sections.FirstOrDefault(x => x.PageSectionId == orderedIds[i]);
            if (s is not null) { s.DisplayOrder = i + 1; s.UpdatedAt = DateTime.UtcNow; }
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<SectionDto>> UpdateHomeSectionsAsync(List<UpdateSectionItem> items, CancellationToken ct = default)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Slug == "home", ct);
        if (page is null) return [];
        var sections = await db.PageSections.Where(s => s.PageId == page.PageId).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            var s = sections.FirstOrDefault(x => x.PageSectionId == item.PageSectionId);
            if (s is null) continue;
            s.DisplayOrder = item.DisplayOrder; s.IsVisible = item.IsVisible;
            if (item.Title is not null) s.Title = item.Title;
            s.StartsAt = item.StartsAt; s.EndsAt = item.EndsAt; s.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        return await SectionsForPageAsync(page.PageId, false, ct);
    }

    // ---- presets ----
    public IReadOnlyList<PresetSummary> ListPresets() => StorefrontPresets.Summaries;

    /// <summary>Replace a page's sections with an industry starter layout.</summary>
    public async Task<PageDetailDto> ApplyPresetAsync(long pageId, string presetKey, CancellationToken ct = default)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.PageId == pageId, ct) ?? throw NotFound();
        var preset = StorefrontPresets.Get(presetKey)
            ?? throw new AppException("Unknown preset.", StatusCodes.Status400BadRequest);

        var existing = await db.PageSections.Where(s => s.PageId == pageId).ToListAsync(ct);
        db.PageSections.RemoveRange(existing);

        var order = 1;
        foreach (var ps in preset.Sections)
        {
            var schema = SectionTypeRegistry.Get(ps.Type);
            if (schema is null) continue;   // defensive: skip anything not in the catalog
            db.PageSections.Add(new PageSection
            {
                PageId = pageId, SectionType = schema.Key, Title = schema.Label,
                Settings = Sanitize(schema.Key, ps.Settings), Blocks = ps.Blocks,
                DisplayOrder = order++, IsVisible = true, CreatedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);
        return (await GetPageAsync(pageId, ct))!;
    }

    // ---- helpers ----
    private async Task<List<SectionDto>> SectionsForPageAsync(long pageId, bool visibleOnly, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.PageSections
            .Where(s => s.PageId == pageId && (!visibleOnly ||
                (s.IsVisible && (s.StartsAt == null || s.StartsAt <= now) && (s.EndsAt == null || s.EndsAt >= now))))
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new SectionDto(s.PageSectionId, s.PageId, s.SectionType, s.Title, s.Settings, s.Blocks,
                s.DisplayOrder, s.IsVisible, s.StartsAt, s.EndsAt))
            .ToListAsync(ct);
    }

    /// <summary>Sanitize any richtext settings fields for the section type (strip scripts/handlers).</summary>
    private string? Sanitize(string sectionType, string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson)) return settingsJson;
        var richKeys = SectionTypeRegistry.RichTextSettingKeys(sectionType).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (richKeys.Count == 0) return settingsJson;
        if (JsonNode.Parse(settingsJson) is not JsonObject obj) return settingsJson;
        foreach (var key in richKeys)
            if (obj[key] is JsonValue v && v.TryGetValue<string>(out var html))
                obj[key] = _sanitizer.Sanitize(html);
        return obj.ToJsonString(JsonOpts);
    }

    private static string DefaultSettings(SectionTypeSchema schema)
    {
        var obj = new JsonObject();
        foreach (var f in schema.Settings.Where(f => f.Default is not null))
            obj[f.Key] = JsonValue.Create(f.Default);
        return obj.ToJsonString(JsonOpts);
    }

    private static string NormalizeSlug(string? slug, string title)
    {
        var s = (string.IsNullOrWhiteSpace(slug) ? title : slug).Trim().ToLowerInvariant();
        s = System.Text.RegularExpressions.Regex.Replace(s, "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(s) ? "page" : s;
    }

    private static PageDto ToDto(Page p) => new(p.PageId, p.Title, p.Slug, p.Type, p.IsPublished, p.MetaTitle, p.MetaDescription);
    private static SectionDto ToDto(PageSection s) => new(s.PageSectionId, s.PageId, s.SectionType, s.Title, s.Settings, s.Blocks, s.DisplayOrder, s.IsVisible, s.StartsAt, s.EndsAt);
    private static AppException NotFound() => new("Not found.", StatusCodes.Status404NotFound);
}
