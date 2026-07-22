using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Support;

public sealed record TicketDto(
    long Id, long TenantId, string Subject, string Status, bool OpenedByPlatform, DateTime CreatedAt,
    DateTime? LastMessageAt, string? StoreName,
    string Axis, string? Reference, string Priority, string? Category, long? AssignedToUserId,
    DateTime? FirstResponseAt, DateTime? ResolvedAt);

/// <summary><paramref name="FromPlatform"/> is derived from <c>AuthorType</c> and kept so existing clients don't change.</summary>
public sealed record TicketMessageDto(long Id, bool FromPlatform, bool IsInternalNote, string Body, DateTime CreatedAt, string AuthorType);
public sealed record TicketThreadDto(TicketDto Ticket, List<TicketMessageDto> Messages);

public interface ISupportService
{
    // Merchant (current tenant, auto-scoped)
    Task<TicketDto> CreateAsync(string subject, string firstMessage, long userId, CancellationToken ct);
    Task<IReadOnlyList<TicketDto>> MyTicketsAsync(CancellationToken ct);
    Task<TicketThreadDto> ThreadAsync(long ticketId, CancellationToken ct);
    Task ReplyAsync(long ticketId, string body, long userId, CancellationToken ct);
    // Platform (cross-tenant)
    Task<IReadOnlyList<TicketDto>> QueueAsync(string? status, CancellationToken ct);
    Task<TicketThreadDto> AdminThreadAsync(long ticketId, CancellationToken ct);
    Task AdminReplyAsync(long ticketId, string body, long adminUserId, bool isInternal, CancellationToken ct);
    Task SetStatusAsync(long ticketId, string status, long adminUserId, CancellationToken ct);
    Task<TicketDto> TriageAsync(long ticketId, string? priority, string? category, long? assignedToUserId, long adminUserId, CancellationToken ct);
}

/// <summary>
/// Support tickets. Merchant methods run in the request's tenant context (global filter auto-scopes).
/// Platform methods read cross-tenant via IgnoreQueryFilters and write through BeginScope so a reply/note
/// lands on the ticket's tenant. Internal notes are never returned to the merchant thread.
/// </summary>
public sealed class SupportService(EcommerceDbContext db, ICurrentTenantService tenant) : ISupportService
{
    private static readonly HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase) { "Open", "Pending", "Closed" };
    private static readonly HashSet<string> Priorities = new(StringComparer.OrdinalIgnoreCase) { "Low", "Normal", "High", "Urgent" };

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
        db.SupportMessages.Add(new SupportMessage
        {
            SupportTicketId = ticketId, AuthorUserId = userId,
            AuthorType = MessageAuthorType.Merchant, FromPlatform = false,
            Body = body, CreatedAt = now,
        });
        ticket.LastMessageAt = now;
        if (ticket.Status == "Closed") { ticket.Status = "Open"; ticket.ResolvedAt = null; }   // a merchant reply reopens
        ticket.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TicketDto>> QueueAsync(string? status, CancellationToken ct)
    {
        // The platform queue is the merchant↔platform axis only — shopper threads belong to the merchant.
        var q = db.SupportTickets.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.Axis == ConversationAxis.MerchantPlatform);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(t => t.Status == status);
        var tickets = await q.OrderByDescending(t => t.LastMessageAt ?? t.CreatedAt).Take(200).ToListAsync(ct);
        var ids = tickets.Select(t => t.TenantId).Distinct().ToList();
        var names = await db.Tenants.Where(t => ids.Contains(t.TenantId)).Select(t => new { t.TenantId, t.Name }).ToListAsync(ct);
        return tickets.Select(t => Map(t, names.FirstOrDefault(n => n.TenantId == t.TenantId)?.Name)).ToList();
    }

    public Task<TicketThreadDto> AdminThreadAsync(long ticketId, CancellationToken ct) => LoadThreadAsync(ticketId, includeInternal: true, crossTenant: true, ct);

    public async Task AdminReplyAsync(long ticketId, string body, long adminUserId, bool isInternal, CancellationToken ct)
    {
        body = (body ?? "").Trim();
        if (body.Length == 0) throw new AppException("Message is empty.", StatusCodes.Status400BadRequest);
        var ticket = await db.SupportTickets.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.SupportTicketId == ticketId, ct)
                     ?? throw new AppException("Ticket not found.", StatusCodes.Status404NotFound);
        var now = DateTime.UtcNow;
        using (tenant.BeginScope(ticket.TenantId))
        {
            db.SupportMessages.Add(new SupportMessage
            {
                SupportTicketId = ticketId, AuthorUserId = adminUserId,
                AuthorType = MessageAuthorType.Platform, FromPlatform = true,
                IsInternalNote = isInternal, Body = body, CreatedAt = now,
            });
            if (!isInternal)
            {
                ticket.LastMessageAt = now;
                ticket.FirstResponseAt ??= now;                                  // first real reply, for SLA
                if (ticket.Status == "Open") ticket.Status = "Pending";           // awaiting merchant
            }
            ticket.UpdatedAt = now;
            db.PlatformAccessLog.Add(new PlatformAccessLog { AdminUserId = adminUserId, TenantId = ticket.TenantId, Action = isInternal ? "SupportNote" : "SupportReply", Detail = $"#{ticketId}", CreatedAt = now });
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task SetStatusAsync(long ticketId, string status, long adminUserId, CancellationToken ct)
    {
        if (!Statuses.Contains(status)) throw new AppException("Invalid status.", StatusCodes.Status400BadRequest);
        var ticket = await db.SupportTickets.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.SupportTicketId == ticketId, ct)
                     ?? throw new AppException("Ticket not found.", StatusCodes.Status404NotFound);
        using (tenant.BeginScope(ticket.TenantId))
        {
            ticket.Status = status;
            ticket.ResolvedAt = string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null;
            ticket.UpdatedAt = DateTime.UtcNow;
            db.PlatformAccessLog.Add(new PlatformAccessLog { AdminUserId = adminUserId, TenantId = ticket.TenantId, Action = "SupportStatus", Detail = $"#{ticketId} → {status}", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<TicketDto> TriageAsync(long ticketId, string? priority, string? category, long? assignedToUserId, long adminUserId, CancellationToken ct)
    {
        if (priority is not null && !Priorities.Contains(priority))
            throw new AppException("Invalid priority.", StatusCodes.Status400BadRequest);

        var ticket = await db.SupportTickets.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.SupportTicketId == ticketId, ct)
                     ?? throw new AppException("Ticket not found.", StatusCodes.Status404NotFound);

        using (tenant.BeginScope(ticket.TenantId))
        {
            if (priority is not null) ticket.Priority = priority;
            if (category is not null) ticket.Category = category.Trim() is { Length: > 0 } c ? c : null;
            if (assignedToUserId is not null) ticket.AssignedToUserId = assignedToUserId == 0 ? null : assignedToUserId;
            ticket.UpdatedAt = DateTime.UtcNow;
            db.PlatformAccessLog.Add(new PlatformAccessLog
            {
                AdminUserId = adminUserId, TenantId = ticket.TenantId, Action = "SupportTriage",
                Detail = $"#{ticketId} → {ticket.Priority}/{ticket.Category ?? "-"}", CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }
        return Map(ticket, null);
    }

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
        return new TicketThreadDto(Map(ticket, null), messages);
    }

    /// <summary>Human-quotable thread id, e.g. <c>TKT-2026-00042</c>.</summary>
    internal static string BuildReference(DateTime createdAt, long id) => $"TKT-{createdAt:yyyy}-{id:D5}";

    private static TicketDto Map(SupportTicket t, string? storeName) =>
        new(t.SupportTicketId, t.TenantId, t.Subject, t.Status, t.OpenedByPlatform, t.CreatedAt, t.LastMessageAt, storeName,
            t.Axis, t.Reference, t.Priority, t.Category, t.AssignedToUserId, t.FirstResponseAt, t.ResolvedAt);
}
