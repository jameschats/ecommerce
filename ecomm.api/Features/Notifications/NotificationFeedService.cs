using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Notifications;

public sealed record NotificationDto(long Id, string Type, string Title, string? Message, string? LinkUrl, bool IsRead, DateTime CreatedAt);

/// <summary>
/// In-app notification feed (the bell). Creates rows + pushes them in real time over SignalR:
/// customer notifications to <c>user-{id}</c>, admin notifications to the <c>admins</c> group.
/// </summary>
public interface INotificationFeedService
{
    Task NotifyUserAsync(long userId, string type, string title, string? message, string? linkUrl, CancellationToken ct = default);
    Task NotifyAdminsAsync(string type, string title, string? message, string? linkUrl, CancellationToken ct = default);
    Task<List<NotificationDto>> ListAsync(long userId, bool isAdmin, int limit, CancellationToken ct = default);
    Task<int> UnreadCountAsync(long userId, bool isAdmin, CancellationToken ct = default);
    Task MarkReadAsync(long userId, bool isAdmin, long id, CancellationToken ct = default);
    Task MarkAllReadAsync(long userId, bool isAdmin, CancellationToken ct = default);
}

public sealed class NotificationFeedService : INotificationFeedService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationFeedService> _logger;

    public NotificationFeedService(EcommerceDbContext db, IHubContext<NotificationHub> hub, ILogger<NotificationFeedService> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public Task NotifyUserAsync(long userId, string type, string title, string? message, string? linkUrl, CancellationToken ct = default)
        => CreateAsync("Customer", userId, type, title, message, linkUrl, ct);

    public Task NotifyAdminsAsync(string type, string title, string? message, string? linkUrl, CancellationToken ct = default)
        => CreateAsync("Admin", null, type, title, message, linkUrl, ct);

    private async Task CreateAsync(string audience, long? userId, string type, string title, string? message, string? linkUrl, CancellationToken ct)
    {
        try
        {
            var n = new Notification
            {
                TenantId = Tenant, UserId = userId, Audience = audience, Type = type,
                Title = title, Message = message, LinkUrl = linkUrl, CreatedAt = DateTime.UtcNow,
            };
            _db.Notifications.Add(n);
            await _db.SaveChangesAsync(ct);

            var dto = ToDto(n);
            if (audience == "Admin")
                await _hub.Clients.Group(NotificationHub.AdminsGroup).SendAsync("notification", dto, ct);
            else if (userId is { } uid)
                await _hub.Clients.Group(NotificationHub.UserGroup(uid)).SendAsync("notification", dto, ct);
        }
        catch (Exception ex)
        {
            // A notification must never break the business flow that triggered it.
            _logger.LogError(ex, "Failed to create {Audience} notification '{Title}'", audience, title);
        }
    }

    public async Task<List<NotificationDto>> ListAsync(long userId, bool isAdmin, int limit, CancellationToken ct = default)
    {
        var q = Scope(userId, isAdmin);
        return await q.OrderByDescending(n => n.NotificationId).Take(Math.Clamp(limit, 1, 50))
            .Select(n => ToDto(n)).ToListAsync(ct);
    }

    public Task<int> UnreadCountAsync(long userId, bool isAdmin, CancellationToken ct = default)
        => Scope(userId, isAdmin).CountAsync(n => !n.IsRead, ct);

    public async Task MarkReadAsync(long userId, bool isAdmin, long id, CancellationToken ct = default)
    {
        var n = await Scope(userId, isAdmin).FirstOrDefaultAsync(x => x.NotificationId == id, ct);
        if (n is null || n.IsRead) return;
        n.IsRead = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(long userId, bool isAdmin, CancellationToken ct = default)
    {
        var unread = await Scope(userId, isAdmin).Where(n => !n.IsRead).ToListAsync(ct);
        foreach (var n in unread) n.IsRead = true;
        if (unread.Count > 0) await _db.SaveChangesAsync(ct);
    }

    /// <summary>Admins see the shared Admin feed; everyone else sees their own Customer notifications.</summary>
    private IQueryable<Notification> Scope(long userId, bool isAdmin) => isAdmin
        ? _db.Notifications.Where(n => n.TenantId == Tenant && n.Audience == "Admin")
        : _db.Notifications.Where(n => n.TenantId == Tenant && n.Audience == "Customer" && n.UserId == userId);

    private static NotificationDto ToDto(Notification n) =>
        new(n.NotificationId, n.Type, n.Title, n.Message, n.LinkUrl, n.IsRead, n.CreatedAt);
}
