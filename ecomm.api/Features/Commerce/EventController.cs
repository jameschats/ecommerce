using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Commerce;

public sealed record EventItem(string Type, long? ProductId, string? Metadata);
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
        new(new[] { "view", "search", "add-to-cart", "remove-from-cart" }, StringComparer.OrdinalIgnoreCase);
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

        foreach (var e in req.Events.Take(MaxPerRequest))
        {
            if (e is null || string.IsNullOrEmpty(e.Type) || !AllowedTypes.Contains(e.Type)) continue;
            buffer.Add(new CustomerEvent
            {
                TenantId = tid,
                UserId = userId,
                SessionId = sid!,
                EventType = e.Type.ToLowerInvariant(),
                ProductId = e.ProductId,
                Metadata = Clean(e.Metadata, 500),
                CreatedAt = now,
            });
        }
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
