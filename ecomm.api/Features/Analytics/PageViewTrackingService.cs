using System.Text.RegularExpressions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;

namespace ecomm.api.Features.Analytics;

public sealed record TrackPageViewRequest(string? VisitorId, string? SessionId, string? Path, string? Referrer);

public interface IPageViewTrackingService
{
    Task TrackAsync(TrackPageViewRequest req, string? ipAddress, string? userAgent, CancellationToken ct = default);
}

public sealed partial class PageViewTrackingService : IPageViewTrackingService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;
    private readonly IGeoLookupService _geo;

    public PageViewTrackingService(EcommerceDbContext db, IGeoLookupService geo)
    {
        _db = db;
        _geo = geo;
    }

    public async Task TrackAsync(TrackPageViewRequest req, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        // A visitor/session id and a path are the minimum needed to mean anything — the
        // beacon always sends them, so their absence means a malformed/hostile request.
        if (string.IsNullOrWhiteSpace(req.VisitorId) || string.IsNullOrWhiteSpace(req.SessionId)
            || string.IsNullOrWhiteSpace(req.Path)) return;
        if (IsBot(userAgent)) return;

        var (country, state, city) = _geo.Lookup(ipAddress);

        _db.PageViews.Add(new PageView
        {
            TenantId = Tenant,
            VisitorId = Truncate(req.VisitorId, 36),
            SessionId = Truncate(req.SessionId, 36),
            Path = Truncate(req.Path, 500),
            Referrer = string.IsNullOrWhiteSpace(req.Referrer) ? null : Truncate(req.Referrer, 500),
            DeviceType = DetectDevice(userAgent),
            Country = country,
            State = state,
            City = city,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static string DetectDevice(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Desktop";
        if (TabletRegex().IsMatch(userAgent)) return "Tablet";
        if (MobileRegex().IsMatch(userAgent)) return "Mobile";
        return "Desktop";
    }

    private static bool IsBot(string? userAgent) =>
        !string.IsNullOrWhiteSpace(userAgent) && BotRegex().IsMatch(userAgent);

    [GeneratedRegex("ipad|tablet|kindle|playbook", RegexOptions.IgnoreCase)]
    private static partial Regex TabletRegex();

    [GeneratedRegex("mobile|android|iphone|ipod|windows phone|blackberry", RegexOptions.IgnoreCase)]
    private static partial Regex MobileRegex();

    [GeneratedRegex("bot|crawl|spider|slurp|bingpreview|facebookexternalhit|headless", RegexOptions.IgnoreCase)]
    private static partial Regex BotRegex();
}
