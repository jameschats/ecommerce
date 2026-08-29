using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Commerce;

public sealed record EventItem(string Type, long? ProductId, string? Metadata, string? Path, string? Referrer);
public sealed record RecordEventsRequest(string SessionId, List<EventItem> Events);

/// <summary>
/// Storefront behavioural-event ingest (AI Commerce data layer). Anonymous-friendly: a first-party
/// visitor id (SessionId) groups a visitor's events before they log in; UserId is captured only when a
/// token is present. Enqueues to an in-memory buffer and returns immediately — the actual DB write is
/// batched by a background job, so this never adds write latency to the storefront.
/// </summary>
[ApiController]
[Route("api/events")]
public sealed class EventController(CustomerEventBuffer buffer, ICurrentTenantService tenant) : ControllerBase
{
    private static readonly HashSet<string> AllowedTypes =
        new(new[] { "view", "search", "add-to-cart", "remove-from-cart", "page" }, StringComparer.OrdinalIgnoreCase);
    private const int MaxPerRequest = 50;

    [HttpPost]
    [AllowAnonymous]
    public IActionResult Record([FromBody] RecordEventsRequest? req)
    {
        if (req?.Events is null || req.Events.Count == 0) return Ok(ApiResponse<object>.Ok(new { }));

        var sid = Clean(req.SessionId, 64);
        if (string.IsNullOrEmpty(sid)) return Ok(ApiResponse<object>.Ok(new { }));   // no visitor id → nothing to group on

        long? userId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;
        var tid = tenant.CurrentTenantId;
        var now = DateTime.UtcNow;

        // Per-request context for traffic analytics (same for every event in the batch).
        var device = DeviceFromUserAgent(Request.Headers.UserAgent.ToString());
        var country = Clean(Request.Headers["CF-IPCountry"].ToString(), 2)?.ToUpperInvariant();   // Cloudflare geo (free)
        if (country is "XX" or "T1") country = null;
        var region = Clean(Request.Headers["CF-Region"].ToString(), 80);
        var city = Clean(Request.Headers["CF-IPCity"].ToString(), 80);

        foreach (var e in req.Events.Take(MaxPerRequest))
        {
            if (e is null || string.IsNullOrEmpty(e.Type) || !AllowedTypes.Contains(e.Type)) continue;
            var isPage = e.Type.Equals("page", StringComparison.OrdinalIgnoreCase);
            buffer.Add(new CustomerEvent
            {
                TenantId = tid,
                UserId = userId,
                SessionId = sid!,
                EventType = e.Type.ToLowerInvariant(),
                ProductId = e.ProductId,
                Metadata = Clean(e.Metadata, 500),
                Path = isPage ? Clean(e.Path, 300) : null,
                Referrer = isPage ? Clean(e.Referrer, 200) : null,
                Device = isPage ? device : null,
                Country = isPage ? country : null,
                Region = isPage ? region : null,
                City = isPage ? city : null,
                CreatedAt = now,
            });
        }
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    private static string DeviceFromUserAgent(string ua)
    {
        if (string.IsNullOrEmpty(ua)) return "desktop";
        if (ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) || ua.Contains("Tablet", StringComparison.OrdinalIgnoreCase)) return "tablet";
        if (ua.Contains("Mobi", StringComparison.OrdinalIgnoreCase) || ua.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "mobile";
        return "desktop";
    }

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
