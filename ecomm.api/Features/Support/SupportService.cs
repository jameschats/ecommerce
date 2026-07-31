using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Support;

public sealed record TicketDto(
    long Id, long TenantId, string Subject, string Status, bool OpenedByPlatform, DateTime CreatedAt,
    DateTime? LastMessageAt, string? StoreName,
    string Axis, string? Reference, string Priority, string? Category, long? AssignedToUserId,
    DateTime? FirstResponseAt, DateTime? ResolvedAt,
    string EscalationTier, string? Tags, string? AssignedToName);

/// <summary><paramref name="FromPlatform"/> is derived from <c>AuthorType</c> and kept so existing clients don't change.</summary>
public sealed record TicketMessageDto(long Id, bool FromPlatform, bool IsInternalNote, string Body, DateTime CreatedAt, string AuthorType);
public sealed record TicketActivityDto(long Id, string Type, string Detail, DateTime CreatedAt);
public sealed record TicketThreadDto(TicketDto Ticket, List<TicketMessageDto> Messages, List<TicketActivityDto> Activity);
public sealed record TicketQueueFilter(string? Status, string? Priority, string? Tier, long? AssignedToUserId, bool Unassigned);
public sealed record AgentDto(long UserId, string Name);

public interface ISupportService
{
    // Merchant (current tenant, auto-scoped)
    Task<TicketDto> CreateAsync(string subject, string firstMessage, long userId, CancellationToken ct);
    Task<IReadOnlyList<TicketDto>> MyTicketsAsync(CancellationToken ct);
    Task<TicketThreadDto> ThreadAsync(long ticketId, CancellationToken ct);
    Task ReplyAsync(long ticketId, string body, long userId, CancellationToken ct);
    // Platform (cross-tenant)
    Task<IReadOnlyList<TicketDto>> QueueAsync(TicketQueueFilter filter, CancellationToken ct);
    Task<TicketThreadDto> AdminThreadAsync(long ticketId, CancellationToken ct);
    Task AdminReplyAsync(long ticketId, string body, long adminUserId, bool isInternal, CancellationToken ct);
    Task<TicketDto> SetStatusAsync(long ticketId, string status, long adminUserId, CancellationToken ct);
    Task<TicketDto> TriageAsync(long ticketId, string? priority, string? category, long? assignedToUserId, long adminUserId, CancellationToken ct);
    Task<TicketDto> EscalateAsync(long ticketId, string? tier, long adminUserId, CancellationToken ct);
    Task<TicketDto> AssignAsync(long ticketId, long? assigneeUserId, long adminUserId, CancellationToken ct);
    Task<TicketDto> SetTagsAsync(long ticketId, string? tags, long adminUserId, CancellationToken ct);
    Task<IReadOnlyList<AgentDto>> AgentsAsync(CancellationToken ct);
}

/// <summary>
/// Support tickets. Merchant methods run in the request's tenant context (global filter auto-scopes).
/// Platform methods read cross-tenant via IgnoreQueryFilters and write through BeginScope so a reply/note
/// lands on the ticket's tenant. Internal notes are never returned to the merchant thread.
/// </summary>
public sealed class SupportService(
    EcommerceDbContext db, ICurrentTenantService tenant, IConversationRealtime realtime) : ISupportService
{
    private static readonly HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase) { "New", "Open", "Pending", "OnHold", "Resolved", "Closed" };
    private static readonly HashSet<string> Priorities = new(StringComparer.OrdinalIgnoreCase) { "Low", "Normal", "High", "Urgent" };
    private static readonly HashSet<string> Tiers = new(StringComparer.OrdinalIgnoreCase) { "L1", "L2", "L3" };
    private static readonly HashSet<string> ResolvedStatuses = new(StringComparer.OrdinalIgnoreCase) { "Resolved", "Closed" };

    public async Task<TicketDto> CreateAsync(string subject, string firstMessage, long userId, CancellationToken ct)
    {
        subject = (subject ?? "").Trim();
        firstMessage = (firstMessage ?? "").Trim();
        if (subject.Length == 0 || firstMessage.Length == 0) throw new AppException("Subject and message are required.", StatusCodes.Status400BadRequest);
        var now = DateTime.UtcNow;
        var ticket = new SupportTicket
        {
            Axis = ConversationAxis.MerchantPlatform,
            Subject = subject, Status = "Open", Priority = "Normal",
            CreatedByUserId = userId, LastMessageAt = now, CreatedAt = now,
        };
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync(ct);   // auto-stamped to current tenant, SupportTicketId assigned

        ticket.Reference = BuildReference(now, ticket.SupportTicketId);
        db.SupportMessages.Add(new SupportMessage
        {
            SupportTicketId = ticket.SupportTicketId, AuthorUserId = userId,
            AuthorType = MessageAuthorType.Merchant, FromPlatform = false,
            Body = firstMessage, CreatedAt = now,
        });
        db.SupportTicketActivities.Add(new SupportTicketActivity
        {
            SupportTicketId = ticket.SupportTicketId, ActorUserId = userId,
            Type = "created", Detail = "Ticket opened", CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);
        return Map(ticket, null);
    }

    public async Task<IReadOnlyList<TicketDto>> MyTicketsAsync(CancellationToken ct) =>
        (await db.SupportTickets.AsNoTracking()
            .Where(t => t.Axis == ConversationAxis.MerchantPlatform)
            .OrderByDescending(t => t.LastMessageAt ?? t.CreatedAt)
            .ToListAsync(ct))
            .Select(t => Map(t, null)).ToList();

    public Task<TicketThreadDto> ThreadAsync(long ticketId, CancellationToken ct) => LoadThreadAsync(ticketId, includeInternal: false, crossTenant: false, ct);

    public async Task ReplyAsync(long ticketId, string body, long userId, CancellationToken ct)
    {
        body = (body ?? "").Trim();
        if (body.Length == 0) throw new AppException("Message is empty.", StatusCodes.Status400BadRequest);
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.SupportTicketId == ticketId, ct)
                     ?? throw new AppException("Ticket not found.", StatusCodes.Status404NotFound);
        var now = DateTime.UtcNow;
        var message = new SupportMessage
        {
            SupportTicketId = ticketId, AuthorUserId = userId,
            AuthorType = MessageAuthorType.Merchant, FromPlatform = false,
            Body = body, CreatedAt = now,
        };
        db.SupportMessages.Add(message);
        ticket.LastMessageAt = now;
        if (ticket.Status == "Closed") { ticket.Status = "Open"; ticket.ResolvedAt = null; }   // a merchant reply reopens
        ticket.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        // Saved first, pushed second — see IConversationRealtime.
        await realtime.MessageAsync(
            new LiveMessageDto(ticketId, message.SupportMessageId, message.AuthorType, body, now), ct);
    }

    public async Task<IReadOnlyList<TicketDto>> QueueAsync(TicketQueueFilter filter, CancellationToken ct)
    {
        // The platform queue is the merchant↔platform axis only — shopper threads belong to the merchant.
        var q = db.SupportTickets.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.Axis == ConversationAxis.MerchantPlatform);

        if (!string.IsNullOrWhiteSpace(filter.Status)) q = q.Where(t => t.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Priority)) q = q.Where(t => t.Priority == filter.Priority);
        if (!string.IsNullOrWhiteSpace(filter.Tier)) q = q.Where(t => t.EscalationTier == filter.Tier);
        if (filter.Unassigned) q = q.Where(t => t.AssignedToUserId == null);
        else if (filter.AssignedToUserId is { } agent) q = q.Where(t => t.AssignedToUserId == agent);

        var tickets = await q.OrderByDescending(t => t.LastMessageAt ?? t.CreatedAt).Take(200).ToListAsync(ct);

        var tenantIds = tickets.Select(t => t.TenantId).Distinct().ToList();
        var names = await db.Tenants.Where(t => tenantIds.Contains(t.TenantId)).Select(t => new { t.TenantId, t.Name }).ToListAsync(ct);
        var agents = await ResolveAgentNamesAsync(tickets, ct);

        return tickets.Select(t => Map(t,
            names.FirstOrDefault(n => n.TenantId == t.TenantId)?.Name,
            t.AssignedToUserId is { } a && agents.TryGetValue(a, out var an) ? an : null)).ToList();
    }

    public async Task<IReadOnlyList<AgentDto>> AgentsAsync(CancellationToken ct) =>
        await (from u in db.Users
               join ur in db.UserRoles on u.UserId equals ur.UserId
               join r in db.Roles on ur.RoleId equals r.RoleId
               where r.NormalizedName == "SUPERADMIN" && !u.IsDeleted && u.IsActive
               orderby u.FullName
               select new AgentDto(u.UserId, u.FullName ?? u.Email ?? $"#{u.UserId}"))
            .ToListAsync(ct);

    private async Task<Dictionary<long, string>> ResolveAgentNamesAsync(IEnumerable<SupportTicket> tickets, CancellationToken ct)
    {
        var ids = tickets.Where(t => t.AssignedToUserId != null).Select(t => t.AssignedToUserId!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, string>();
        return await db.Users.IgnoreQueryFilters().Where(u => ids.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName ?? u.Email ?? $"#{u.UserId}", ct);
    }

    public Task<TicketThreadDto> AdminThreadAsync(long ticketId, CancellationToken ct) => LoadThreadAsync(ticketId, includeInternal: true, crossTenant: true, ct);

    public async Task AdminReplyAsync(long ticketId, string body, long adminUserId, bool isInternal, CancellationToken ct)
    {
        body = (body ?? "").Trim();
        if (body.Length == 0) throw new AppException("Message is empty.", StatusCodes.Status400BadRequest);
        var ticket = await db.SupportTickets.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.SupportTicketId == ticketId, ct)
                     ?? throw new AppException("Ticket not found.", StatusCodes.Status404NotFound);
        var now = DateTime.UtcNow;
        SupportMessage? message = null;
        using (tenant.BeginScope(ticket.TenantId))
        {
            message = new SupportMessage
            {
                SupportTicketId = ticketId, AuthorUserId = adminUserId,
                AuthorType = MessageAuthorType.Platform, FromPlatform = true,
                IsInternalNote = isInternal, Body = body, CreatedAt = now,
            };
            db.SupportMessages.Add(message);
            if (!isInternal)
            {
                ticket.LastMessageAt = now;
                ticket.FirstResponseAt ??= now;                                  // first real reply, for SLA
                if (ticket.Status is "New" or "Open") ticket.Status = "Pending";  // awaiting merchant
            }
            ticket.UpdatedAt = now;
            db.PlatformAccessLog.Add(new PlatformAccessLog { AdminUserId = adminUserId, TenantId = ticket.TenantId, Action = isInternal ? "SupportNote" : "SupportReply", Detail = $"#{ticketId}", CreatedAt = now });
            await db.SaveChangesAsync(ct);
        }

        // Internal notes are platform-only and must never be pushed to a merchant's open thread.
        if (!isInternal)
            await realtime.MessageAsync(
                new LiveMessageDto(ticketId, message.SupportMessageId, message.AuthorType, body, now), ct);
    }

    public async Task<TicketDto> SetStatusAsync(long ticketId, string status, long adminUserId, CancellationToken ct)
    {
        if (!Statuses.Contains(status)) throw new AppException("Invalid status.", StatusCodes.Status400BadRequest);
        var ticket = await LoadForAdminAsync(ticketId, ct);
        using (tenant.BeginScope(ticket.TenantId))
        {
            var from = ticket.Status;
            if (!string.Equals(from, status, StringComparison.OrdinalIgnoreCase))
            {
                ticket.Status = status;
                ticket.ResolvedAt = ResolvedStatuses.Contains(status) ? DateTime.UtcNow : null;
                ticket.UpdatedAt = DateTime.UtcNow;
                LogActivity(ticket, adminUserId, "status", $"{from} → {status}");
                Audit(adminUserId, ticket, "SupportStatus", $"#{ticketId} → {status}");
                await db.SaveChangesAsync(ct);
            }
        }
        return Map(ticket, null);
    }

    public async Task<TicketDto> TriageAsync(long ticketId, string? priority, string? category, long? assignedToUserId, long adminUserId, CancellationToken ct)
    {
        if (priority is not null && !Priorities.Contains(priority))
            throw new AppException("Invalid priority.", StatusCodes.Status400BadRequest);

        var ticket = await LoadForAdminAsync(ticketId, ct);
        using (tenant.BeginScope(ticket.TenantId))
        {
            if (priority is not null && !string.Equals(ticket.Priority, priority, StringComparison.OrdinalIgnoreCase))
            {
                LogActivity(ticket, adminUserId, "priority", $"{ticket.Priority} → {priority}");
                ticket.Priority = priority;
            }
            if (category is not null)
            {
                var c = category.Trim() is { Length: > 0 } v ? v : null;
                if (!string.Equals(ticket.Category, c, StringComparison.OrdinalIgnoreCase))
                {
                    LogActivity(ticket, adminUserId, "category", c is null ? "Category cleared" : $"Category: {c}");
                    ticket.Category = c;
                }
            }
            if (assignedToUserId is not null)
                await ApplyAssignmentAsync(ticket, assignedToUserId == 0 ? null : assignedToUserId, adminUserId, ct);

            ticket.UpdatedAt = DateTime.UtcNow;
            Audit(adminUserId, ticket, "SupportTriage", $"#{ticketId} → {ticket.Priority}/{ticket.Category ?? "-"}");
            await db.SaveChangesAsync(ct);
        }
        return Map(ticket, null);
    }

    public async Task<TicketDto> EscalateAsync(long ticketId, string? tier, long adminUserId, CancellationToken ct)
    {
        var ticket = await LoadForAdminAsync(ticketId, ct);
        // No tier given → step up one level; otherwise set the named tier.
        var target = tier is { Length: > 0 }
            ? (Tiers.Contains(tier) ? tier.ToUpperInvariant() : throw new AppException("Invalid tier.", StatusCodes.Status400BadRequest))
            : ticket.EscalationTier switch { "L1" => "L2", "L2" => "L3", _ => "L3" };

        using (tenant.BeginScope(ticket.TenantId))
        {
            if (!string.Equals(ticket.EscalationTier, target, StringComparison.OrdinalIgnoreCase))
            {
                var from = ticket.EscalationTier;
                ticket.EscalationTier = target;
                // Escalating bumps priority to at least High — an escalated ticket shouldn't sit at Normal.
                if (ticket.Priority is "Low" or "Normal") ticket.Priority = "High";
                if (ticket.Status is "New") ticket.Status = "Open";
                ticket.UpdatedAt = DateTime.UtcNow;
                LogActivity(ticket, adminUserId, "escalated", $"Escalated {from} → {target}");
                Audit(adminUserId, ticket, "SupportEscalate", $"#{ticketId} {from}→{target}");
                await db.SaveChangesAsync(ct);
            }
        }
        return Map(ticket, null);
    }

    public async Task<TicketDto> AssignAsync(long ticketId, long? assigneeUserId, long adminUserId, CancellationToken ct)
    {
        var ticket = await LoadForAdminAsync(ticketId, ct);
        using (tenant.BeginScope(ticket.TenantId))
        {
            await ApplyAssignmentAsync(ticket, assigneeUserId is 0 ? null : assigneeUserId, adminUserId, ct);
            ticket.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Map(ticket, null);
    }

    public async Task<TicketDto> SetTagsAsync(long ticketId, string? tags, long adminUserId, CancellationToken ct)
    {
        var ticket = await LoadForAdminAsync(ticketId, ct);
        // Normalise to a clean comma list.
        var cleaned = string.IsNullOrWhiteSpace(tags)
            ? null
            : string.Join(", ", tags.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));

        using (tenant.BeginScope(ticket.TenantId))
        {
            if (!string.Equals(ticket.Tags, cleaned, StringComparison.OrdinalIgnoreCase))
            {
                ticket.Tags = cleaned;
                ticket.UpdatedAt = DateTime.UtcNow;
                LogActivity(ticket, adminUserId, "tags", cleaned is null ? "Tags cleared" : $"Tags: {cleaned}");
                await db.SaveChangesAsync(ct);
            }
        }
        return Map(ticket, null);
    }

    /// <summary>Assigns/unassigns, validating the assignee is an active platform agent, and logs it. Caller saves.</summary>
    private async Task ApplyAssignmentAsync(SupportTicket ticket, long? assigneeUserId, long adminUserId, CancellationToken ct)
    {
        if (ticket.AssignedToUserId == assigneeUserId) return;

        string detail;
        if (assigneeUserId is { } id)
        {
            var agent = await (from u in db.Users.IgnoreQueryFilters()
                               join ur in db.UserRoles on u.UserId equals ur.UserId
                               join r in db.Roles on ur.RoleId equals r.RoleId
                               where u.UserId == id && r.NormalizedName == "SUPERADMIN" && !u.IsDeleted && u.IsActive
                               select u.FullName ?? u.Email ?? $"#{u.UserId}").FirstOrDefaultAsync(ct)
                        ?? throw new AppException("That person isn't a support agent.", StatusCodes.Status400BadRequest);
            detail = $"Assigned to {agent}";
        }
        else
        {
            detail = "Unassigned";
        }
        ticket.AssignedToUserId = assigneeUserId;
        LogActivity(ticket, adminUserId, "assignee", detail);
    }

    private void LogActivity(SupportTicket ticket, long? actorUserId, string type, string detail) =>
        db.SupportTicketActivities.Add(new SupportTicketActivity
        {
            SupportTicketId = ticket.SupportTicketId, ActorUserId = actorUserId,
            Type = type, Detail = detail, CreatedAt = DateTime.UtcNow,
        });

    private void Audit(long adminUserId, SupportTicket ticket, string action, string detail) =>
        db.PlatformAccessLog.Add(new PlatformAccessLog
        {
            AdminUserId = adminUserId, TenantId = ticket.TenantId, Action = action, Detail = detail, CreatedAt = DateTime.UtcNow,
        });

    private async Task<SupportTicket> LoadForAdminAsync(long ticketId, CancellationToken ct) =>
        await db.SupportTickets.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.SupportTicketId == ticketId, ct)
        ?? throw new AppException("Ticket not found.", StatusCodes.Status404NotFound);

    private async Task<TicketThreadDto> LoadThreadAsync(long ticketId, bool includeInternal, bool crossTenant, CancellationToken ct)
    {
        var tickets = crossTenant ? db.SupportTickets.IgnoreQueryFilters() : db.SupportTickets;
        var ticket = await tickets.AsNoTracking().FirstOrDefaultAsync(t => t.SupportTicketId == ticketId, ct)
                     ?? throw new AppException("Ticket not found.", StatusCodes.Status404NotFound);
        var msgs = crossTenant ? db.SupportMessages.IgnoreQueryFilters() : db.SupportMessages;
        var messages = await msgs.AsNoTracking()
            .Where(m => m.SupportTicketId == ticketId && (includeInternal || !m.IsInternalNote))
            .OrderBy(m => m.SupportMessageId)
            .Select(m => new TicketMessageDto(
                m.SupportMessageId, m.AuthorType == MessageAuthorType.Platform, m.IsInternalNote,
                m.Body, m.CreatedAt, m.AuthorType))
            .ToListAsync(ct);

        // Activity trail only goes to the platform agent view; the merchant sees just their thread.
        var activity = includeInternal
            ? await (crossTenant ? db.SupportTicketActivities.IgnoreQueryFilters() : db.SupportTicketActivities).AsNoTracking()
                .Where(a => a.SupportTicketId == ticketId)
                .OrderBy(a => a.SupportTicketActivityId)
                .Select(a => new TicketActivityDto(a.SupportTicketActivityId, a.Type, a.Detail, a.CreatedAt))
                .ToListAsync(ct)
            : new List<TicketActivityDto>();

        var assigneeName = ticket.AssignedToUserId is { } aid
            ? await db.Users.IgnoreQueryFilters().Where(u => u.UserId == aid)
                .Select(u => u.FullName ?? u.Email).FirstOrDefaultAsync(ct)
            : null;

        return new TicketThreadDto(Map(ticket, null, assigneeName), messages, activity);
    }

    /// <summary>Human-quotable thread id, e.g. <c>TKT-2026-00042</c>.</summary>
    internal static string BuildReference(DateTime createdAt, long id) => $"TKT-{createdAt:yyyy}-{id:D5}";

    private static TicketDto Map(SupportTicket t, string? storeName, string? assigneeName = null) =>
        new(t.SupportTicketId, t.TenantId, t.Subject, t.Status, t.OpenedByPlatform, t.CreatedAt, t.LastMessageAt, storeName,
            t.Axis, t.Reference, t.Priority, t.Category, t.AssignedToUserId, t.FirstResponseAt, t.ResolvedAt,
            t.EscalationTier, t.Tags, assigneeName);
}
