using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.SuperAdmin;

public sealed record AnnouncementDto(long Id, string Title, string Body, string Level, bool IsActive, DateTime? StartsAt, DateTime? EndsAt, DateTime CreatedAt);
public sealed record AnnouncementUpsert(string Title, string Body, string Level, DateTime? StartsAt, DateTime? EndsAt);

public interface IPlatformAnnouncementService
{
    Task<IReadOnlyList<AnnouncementDto>> ListAllAsync(CancellationToken ct);
    Task<IReadOnlyList<AnnouncementDto>> ActiveAsync(DateTime now, CancellationToken ct);
    Task<AnnouncementDto> CreateAsync(AnnouncementUpsert req, CancellationToken ct);
    Task SetActiveAsync(long id, bool active, CancellationToken ct);
}

/// <summary>Platform-wide broadcasts. Super-admin authors them; every merchant admin reads the active set.</summary>
public sealed class PlatformAnnouncementService(EcommerceDbContext db) : IPlatformAnnouncementService
{
    private static readonly HashSet<string> Levels = new(StringComparer.OrdinalIgnoreCase) { "info", "warning", "critical" };

    public async Task<IReadOnlyList<AnnouncementDto>> ListAllAsync(CancellationToken ct) =>
        await db.PlatformAnnouncements.AsNoTracking().OrderByDescending(a => a.PlatformAnnouncementId).Select(Map).ToListAsync(ct);

    public async Task<IReadOnlyList<AnnouncementDto>> ActiveAsync(DateTime now, CancellationToken ct) =>
        await db.PlatformAnnouncements.AsNoTracking()
            .Where(a => a.IsActive && (a.StartsAt == null || a.StartsAt <= now) && (a.EndsAt == null || a.EndsAt >= now))
            .OrderByDescending(a => a.PlatformAnnouncementId).Select(Map).ToListAsync(ct);

    public async Task<AnnouncementDto> CreateAsync(AnnouncementUpsert req, CancellationToken ct)
    {
        var title = (req.Title ?? "").Trim();
        var body = (req.Body ?? "").Trim();
        if (title.Length == 0 || body.Length == 0) throw new AppException("Title and body are required.", StatusCodes.Status400BadRequest);
        var level = Levels.Contains(req.Level ?? "") ? req.Level!.ToLowerInvariant() : "info";
        var a = new PlatformAnnouncement { Title = title, Body = body, Level = level, IsActive = true, StartsAt = req.StartsAt, EndsAt = req.EndsAt, CreatedAt = DateTime.UtcNow };
        db.PlatformAnnouncements.Add(a);
        await db.SaveChangesAsync(ct);
        return new AnnouncementDto(a.PlatformAnnouncementId, a.Title, a.Body, a.Level, a.IsActive, a.StartsAt, a.EndsAt, a.CreatedAt);
    }

    public async Task SetActiveAsync(long id, bool active, CancellationToken ct)
    {
        var a = await db.PlatformAnnouncements.FirstOrDefaultAsync(x => x.PlatformAnnouncementId == id, ct)
                ?? throw new AppException("Announcement not found.", StatusCodes.Status404NotFound);
        a.IsActive = active;
        a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static readonly System.Linq.Expressions.Expression<Func<PlatformAnnouncement, AnnouncementDto>> Map =
        a => new AnnouncementDto(a.PlatformAnnouncementId, a.Title, a.Body, a.Level, a.IsActive, a.StartsAt, a.EndsAt, a.CreatedAt);
}
