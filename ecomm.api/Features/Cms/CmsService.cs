using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Cms;

public sealed record SectionDto(long PageSectionId, string SectionType, string? Title, int DisplayOrder, bool IsVisible);
public sealed record UpdateSectionItem(long PageSectionId, int DisplayOrder, bool IsVisible, string? Title);
public sealed record UpdateSectionsRequest(List<UpdateSectionItem> Sections);

public interface ICmsService
{
    Task<List<SectionDto>> GetHomeSectionsAsync(bool visibleOnly, CancellationToken ct = default);
    Task<List<SectionDto>> UpdateHomeSectionsAsync(List<UpdateSectionItem> items, CancellationToken ct = default);
}

public sealed class CmsService : ICmsService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;

    public CmsService(EcommerceDbContext db) => _db = db;

    public async Task<List<SectionDto>> GetHomeSectionsAsync(bool visibleOnly, CancellationToken ct = default)
    {
        var pageId = await HomePageId(ct);
        if (pageId is null) return [];

        return await _db.PageSections
            .Where(s => s.PageId == pageId && (!visibleOnly || s.IsVisible))
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new SectionDto(s.PageSectionId, s.SectionType, s.Title, s.DisplayOrder, s.IsVisible))
            .ToListAsync(ct);
    }

    public async Task<List<SectionDto>> UpdateHomeSectionsAsync(List<UpdateSectionItem> items, CancellationToken ct = default)
    {
        var pageId = await HomePageId(ct);
        if (pageId is null) return [];

        var sections = await _db.PageSections.Where(s => s.PageId == pageId).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            var section = sections.FirstOrDefault(s => s.PageSectionId == item.PageSectionId);
            if (section is null) continue;
            section.DisplayOrder = item.DisplayOrder;
            section.IsVisible = item.IsVisible;
            if (item.Title is not null) section.Title = item.Title;
            section.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(ct);
        return await GetHomeSectionsAsync(false, ct);
    }

    private Task<long?> HomePageId(CancellationToken ct) =>
        _db.Pages.Where(p => p.TenantId == Tenant && p.Slug == "home")
            .Select(p => (long?)p.PageId).FirstOrDefaultAsync(ct);
}
