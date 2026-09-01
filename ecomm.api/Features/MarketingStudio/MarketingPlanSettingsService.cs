using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>Per-channel row in the plan matrix. <c>connected</c> reflects the live MS1 connection.</summary>
public sealed record ChannelPrefDto(
    string Platform, string DisplayName, bool Connected,
    bool Enabled, bool AllowText, bool AllowPoster, bool AllowVideo);

/// <summary>The weekly-plan preferences screen: cadence + the per-channel matrix (connected channels only).</summary>
public sealed record MarketingPlanSettingsDto(
    int TextPerWeek, int PostersPerWeek, int VideosPerWeek,
    int WeekStartDay, int DefaultPostHour, bool AutoRecur,
    IReadOnlyList<ChannelPrefDto> Channels);

public interface IMarketingPlanSettingsService
{
    Task<MarketingPlanSettingsDto> GetAsync(CancellationToken ct = default);
    Task<MarketingPlanSettingsDto> SaveAsync(MarketingPlanSettingsDto req, CancellationToken ct = default);
}

/// <summary>
/// Weekly-plan preferences (MS2 sub-step 1). Holds the cadence (how many of each type per week, when)
/// and the per-channel × per-type matrix. The matrix is surfaced only for channels the merchant has
/// actually connected (via <see cref="ISocialConnectionService"/>), so unchecking "poster on Instagram"
/// is a real, saved preference. Depends on the connection service through its interface (module stays
/// self-contained).
/// </summary>
public sealed class MarketingPlanSettingsService(EcommerceDbContext db, ISocialConnectionService connections)
    : IMarketingPlanSettingsService
{
    public async Task<MarketingPlanSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var s = await db.MarketingPlanSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var prefs = await db.MarketingChannelPrefs.AsNoTracking().ToListAsync(ct);
        var prefByPlatform = prefs.ToDictionary(p => p.Platform, StringComparer.OrdinalIgnoreCase);

        // Only channels the merchant has connected appear in the matrix.
        var connected = (await connections.ListAsync(ct))
            .Where(c => c.Status is "connected" or "expired")
            .Select(c => (c.Platform, c.DisplayName))
            .ToList();

        var channels = connected.Select(c =>
        {
            prefByPlatform.TryGetValue(c.Platform, out var p);
            return new ChannelPrefDto(c.Platform, c.DisplayName, true,
                p?.Enabled ?? true, p?.AllowText ?? true, p?.AllowPoster ?? true, p?.AllowVideo ?? true);
        }).ToList();

        return new MarketingPlanSettingsDto(
            s?.TextPerWeek ?? 3, s?.PostersPerWeek ?? 2, s?.VideosPerWeek ?? 0,
            s?.WeekStartDay ?? 1, s?.DefaultPostHour ?? 10, s?.AutoRecur ?? false, channels);
    }

    public async Task<MarketingPlanSettingsDto> SaveAsync(MarketingPlanSettingsDto req, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var s = await db.MarketingPlanSettings.FirstOrDefaultAsync(ct);
        if (s is null)
        {
            s = new MarketingPlanSettings { CreatedAt = now };
            db.MarketingPlanSettings.Add(s);
        }
        else { s.UpdatedAt = now; }

        s.TextPerWeek = Math.Clamp(req.TextPerWeek, 0, 50);
        s.PostersPerWeek = Math.Clamp(req.PostersPerWeek, 0, 50);
        s.VideosPerWeek = Math.Clamp(req.VideosPerWeek, 0, 50);
        s.WeekStartDay = Math.Clamp(req.WeekStartDay, 0, 6);
        s.DefaultPostHour = Math.Clamp(req.DefaultPostHour, 0, 23);
        s.AutoRecur = req.AutoRecur;

        // Upsert a pref row per submitted channel (only known platforms).
        var existing = await db.MarketingChannelPrefs.ToListAsync(ct);
        var byPlatform = existing.ToDictionary(p => p.Platform, StringComparer.OrdinalIgnoreCase);
        foreach (var c in req.Channels ?? [])
        {
            if (!SocialPlatforms.IsKnown(c.Platform)) continue;
            if (!byPlatform.TryGetValue(c.Platform, out var row))
            {
                row = new MarketingChannelPref { Platform = SocialPlatforms.Get(c.Platform)!.Key };
                db.MarketingChannelPrefs.Add(row);
            }
            row.Enabled = c.Enabled;
            row.AllowText = c.AllowText;
            row.AllowPoster = c.AllowPoster;
            row.AllowVideo = c.AllowVideo;
            row.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }
}
