using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Ganss.Xss;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Cms;

public sealed record ContentSectionDto(
    long SectionId, string SectionType, string? Title, string? Content, int DisplayOrder, bool IsVisible);

public sealed record ContentPageDto(
    long PageId, string Title, string Slug, bool IsPublished,
    string? MetaTitle, string? MetaDescription, List<ContentSectionDto> Sections);

public sealed record SavePageRequest(string Title, bool IsPublished, string? MetaTitle, string? MetaDescription);

public sealed record SaveSectionRequest(string SectionType, string? Title, string? Content, bool IsVisible);

public interface IContentPageService
{
    Task<ContentPageDto?> GetPublishedAsync(string slug, CancellationToken ct = default);
    Task<List<ContentPageDto>> ListAsync(CancellationToken ct = default);
    Task<ContentPageDto?> GetForAdminAsync(string slug, CancellationToken ct = default);
    Task<ContentPageDto?> SavePageAsync(string slug, SavePageRequest req, CancellationToken ct = default);
    Task<ContentPageDto?> AddSectionAsync(string slug, SaveSectionRequest req, CancellationToken ct = default);
    Task<ContentPageDto?> UpdateSectionAsync(long sectionId, SaveSectionRequest req, CancellationToken ct = default);
    Task<ContentPageDto?> DeleteSectionAsync(long sectionId, CancellationToken ct = default);
    Task<ContentPageDto?> ReorderAsync(string slug, List<long> sectionIds, CancellationToken ct = default);
}

/// <summary>
/// The editable content pages — About, FAQ, Buying guide, Contact.
///
/// Separate from CmsService, which arranges the home page's product rails and knows nothing
/// about text. Sections are typed (055): Prose carries rich HTML, Faq is a question and its
/// answer, and Stats / Cards / Cta carry small JSON payloads so About keeps its layout rather
/// than being flattened into one blob.
/// </summary>
public sealed class ContentPageService : IContentPageService
{
    private const long Tenant = 1;

    /// <summary>Types the admin screen can produce. Anything else is refused rather than stored.</summary>
    private static readonly string[] SectionTypes = { "Prose", "Faq", "Stats", "Cards", "Cta" };

    /// <summary>
    /// Built once and reused: allows exactly the markup the editor's toolbar produces and drops
    /// everything else.
    ///
    /// Content here is written by staff holding cms.manage, not by the public, so this is not
    /// the last line of defence — but it is the difference between a pasted-in script tag being
    /// inert text and it running for every visitor. Sanitising on the way *in* means what is
    /// stored is already safe, so the storefront can render it without each page having to
    /// remember to.
    /// </summary>
    private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

    private static HtmlSanitizer BuildSanitizer()
    {
        var s = new HtmlSanitizer();
        s.AllowedTags.Clear();
        foreach (var t in new[]
        {
            "p", "br", "strong", "b", "em", "i", "u", "h2", "h3", "h4", "ul", "ol", "li", "a", "blockquote",
            "table", "thead", "tbody", "tr", "th", "td",
        })
            s.AllowedTags.Add(t);

        s.AllowedAttributes.Clear();
        s.AllowedAttributes.Add("href");
        s.AllowedAttributes.Add("target");
        s.AllowedAttributes.Add("rel");

        s.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "http", "https", "mailto", "tel" })
            s.AllowedSchemes.Add(scheme);

        s.AllowedCssProperties.Clear();   // no inline styling: the site's own CSS decides how it looks
        return s;
    }

    private readonly EcommerceDbContext _db;
    public ContentPageService(EcommerceDbContext db) => _db = db;

    // ---------------- reads ----------------

    public async Task<ContentPageDto?> GetPublishedAsync(string slug, CancellationToken ct = default)
    {
        var page = await _db.Pages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == Tenant && p.Slug == slug && p.IsPublished, ct);
        if (page is null) return null;

        var sections = await _db.PageSections.AsNoTracking()
            .Where(s => s.PageId == page.PageId && s.IsVisible)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);

        return Map(page, sections);
    }

    public async Task<List<ContentPageDto>> ListAsync(CancellationToken ct = default)
    {
        // The home page is arranged by its own screen and has no editable prose, so it is not
        // offered here — opening it in a text editor would only invite confusion.
        var pages = await _db.Pages.AsNoTracking()
            .Where(p => p.TenantId == Tenant && p.Type != "Home")
            .OrderBy(p => p.PageId)
            .ToListAsync(ct);

        return pages.Select(p => Map(p, new List<PageSection>())).ToList();
    }

    public async Task<ContentPageDto?> GetForAdminAsync(string slug, CancellationToken ct = default)
    {
        var page = await _db.Pages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == Tenant && p.Slug == slug, ct);
        if (page is null) return null;

        // Hidden sections included: admin needs to see what is switched off in order to switch
        // it back on.
        var sections = await _db.PageSections.AsNoTracking()
            .Where(s => s.PageId == page.PageId)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);

        return Map(page, sections);
    }

    // ---------------- writes ----------------

    public async Task<ContentPageDto?> SavePageAsync(string slug, SavePageRequest req, CancellationToken ct = default)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.TenantId == Tenant && p.Slug == slug, ct);
        if (page is null) return null;

        if (string.IsNullOrWhiteSpace(req.Title)) throw new AppException("The page needs a title.");

        page.Title = req.Title.Trim();
        page.IsPublished = req.IsPublished;
        page.MetaTitle = Trim(req.MetaTitle);
        page.MetaDescription = Trim(req.MetaDescription);
        page.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetForAdminAsync(slug, ct);
    }

    public async Task<ContentPageDto?> AddSectionAsync(string slug, SaveSectionRequest req, CancellationToken ct = default)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.TenantId == Tenant && p.Slug == slug, ct);
        if (page is null) return null;

        var type = RequireType(req.SectionType);
        var next = await _db.PageSections.Where(s => s.PageId == page.PageId)
            .Select(s => (int?)s.DisplayOrder).MaxAsync(ct) ?? 0;

        _db.PageSections.Add(new PageSection
        {
            PageId = page.PageId,
            SectionType = type,
            Title = Trim(req.Title),
            Content = Clean(type, req.Content),
            DisplayOrder = next + 1,
            IsVisible = req.IsVisible,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return await GetForAdminAsync(slug, ct);
    }

    public async Task<ContentPageDto?> UpdateSectionAsync(long sectionId, SaveSectionRequest req, CancellationToken ct = default)
    {
        var section = await _db.PageSections.FirstOrDefaultAsync(s => s.PageSectionId == sectionId, ct);
        if (section is null) return null;

        var type = RequireType(req.SectionType);
        section.SectionType = type;
        section.Title = Trim(req.Title);
        section.Content = Clean(type, req.Content);
        section.IsVisible = req.IsVisible;
        section.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await ByPageIdAsync(section.PageId, ct);
    }

    public async Task<ContentPageDto?> DeleteSectionAsync(long sectionId, CancellationToken ct = default)
    {
        var section = await _db.PageSections.FirstOrDefaultAsync(s => s.PageSectionId == sectionId, ct);
        if (section is null) return null;

        var pageId = section.PageId;
        _db.PageSections.Remove(section);
        await _db.SaveChangesAsync(ct);
        return await ByPageIdAsync(pageId, ct);
    }

    public async Task<ContentPageDto?> ReorderAsync(string slug, List<long> sectionIds, CancellationToken ct = default)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.TenantId == Tenant && p.Slug == slug, ct);
        if (page is null) return null;

        var sections = await _db.PageSections.Where(s => s.PageId == page.PageId).ToListAsync(ct);

        // Only the ids actually on this page are honoured, and anything the caller left out
        // keeps its place at the end rather than collapsing to order 0.
        var order = 0;
        foreach (var id in sectionIds ?? [])
        {
            var match = sections.FirstOrDefault(s => s.PageSectionId == id);
            if (match is null) continue;
            match.DisplayOrder = ++order;
        }
        foreach (var left in sections.Where(s => (sectionIds ?? []).All(id => id != s.PageSectionId)))
            left.DisplayOrder = ++order;

        await _db.SaveChangesAsync(ct);
        return await GetForAdminAsync(slug, ct);
    }

    // ---------------- helpers ----------------

    private async Task<ContentPageDto?> ByPageIdAsync(long pageId, CancellationToken ct)
    {
        var slug = await _db.Pages.AsNoTracking().Where(p => p.PageId == pageId)
            .Select(p => p.Slug).FirstOrDefaultAsync(ct);
        return slug is null ? null : await GetForAdminAsync(slug, ct);
    }

    private static string RequireType(string? type)
    {
        var t = (type ?? "").Trim();
        if (!SectionTypes.Contains(t))
            throw new AppException($"Unknown section type '{t}'.");
        return t;
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// Prose is sanitised HTML. The others carry JSON, which must not go through an HTML
    /// sanitiser — it would mangle the payload — so it is validated as JSON instead and stored
    /// as-is, then rendered as text by the storefront rather than as markup.
    /// </summary>
    private static string? Clean(string type, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        if (type is "Prose" or "Faq")
            return Sanitizer.Sanitize(content);

        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(content);
            return content.Trim();
        }
        catch (System.Text.Json.JsonException)
        {
            throw new AppException($"The {type} section's content is not valid JSON.");
        }
    }

    private static ContentPageDto Map(Page p, List<PageSection> sections) =>
        new(p.PageId, p.Title, p.Slug, p.IsPublished, p.MetaTitle, p.MetaDescription,
            sections.Select(s => new ContentSectionDto(
                s.PageSectionId, s.SectionType, s.Title, s.Content, s.DisplayOrder, s.IsVisible)).ToList());
}
