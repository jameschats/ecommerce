using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Notifications;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Support;

public sealed record StartConversationRequest(
    string Subject, string Message, string? Email, string? Name, long? OrderId, long? ProductId);

public sealed record ConversationDto(
    long Id, string? Reference, string Subject, string Status, string? ShopperEmail,
    long? OrderId, long? ProductId, DateTime CreatedAt, DateTime? LastMessageAt, DateTime? FirstResponseAt);

public sealed record ConversationMessageDto(long Id, string AuthorType, string Body, DateTime CreatedAt);
public sealed record ConversationThreadDto(ConversationDto Conversation, List<ConversationMessageDto> Messages, string? ReplyToken);

public interface IShopperConversationService
{
    Task<ConversationThreadDto> StartAsync(StartConversationRequest req, long? shopperUserId, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationDto>> MineAsync(long shopperUserId, CancellationToken ct = default);
    Task<ConversationThreadDto> GetForShopperAsync(long id, long shopperUserId, CancellationToken ct = default);
    Task<ConversationThreadDto> GetByTokenAsync(string token, CancellationToken ct = default);
    Task ReplyAsShopperAsync(long id, string body, long? shopperUserId, string? token, CancellationToken ct = default);

    // Merchant inbox
    Task<PagedResult<ConversationDto>> InboxAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<ConversationThreadDto> GetForMerchantAsync(long id, CancellationToken ct = default);
    Task ReplyAsMerchantAsync(long id, string body, long merchantUserId, CancellationToken ct = default);
    Task SetStatusAsync(long id, string status, CancellationToken ct = default);
    Task<int> OpenCountAsync(CancellationToken ct = default);
}

/// <summary>
/// Shopper↔merchant conversations — the <see cref="ConversationAxis.ShopperMerchant"/> half of the
/// engine. Rides the same tables as <see cref="SupportService"/> (which owns the merchant↔platform
/// axis); the two are kept as separate services because their auth models differ completely, not
/// because the data differs.
///
/// Anonymous shoppers reach their own thread through a <b>signed reply token</b> (DataProtection,
/// same mechanism as the encrypted tenant secrets). The token names one conversation and nothing
/// else, so a leaked link exposes one thread rather than an account.
/// </summary>
public sealed class ShopperConversationService(
    EcommerceDbContext db, IDataProtectionProvider dp, INotificationFeedService feed) : IShopperConversationService
{
    public const string ProtectorPurpose = "ecomm.conversation.reply.v1";
    private static readonly HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase) { "Open", "Pending", "Closed" };

    private IDataProtector Protector => dp.CreateProtector(ProtectorPurpose);

    public async Task<ConversationThreadDto> StartAsync(StartConversationRequest req, long? shopperUserId, CancellationToken ct = default)
    {
        var subject = Trim(req.Subject, 200);
        var body = Trim(req.Message, 4000);
        if (subject.Length == 0 || body.Length == 0)
            throw new AppException("Please add a subject and a message.", StatusCodes.Status400BadRequest);

        var email = Trim(req.Email, 200);
        if (shopperUserId is { } uid)
            email = await db.Users.Where(u => u.UserId == uid).Select(u => u.Email).FirstOrDefaultAsync(ct) ?? email;
        if (email.Length == 0 || !email.Contains('@'))
            throw new AppException("We need a valid email address to reply to.", StatusCodes.Status400BadRequest);

        // Only link an order the shopper actually owns — never trust the id off the wire.
        long? orderId = null;
        if (req.OrderId is { } oid && shopperUserId is { } ownerId)
            orderId = await db.Orders.AnyAsync(o => o.OrderId == oid && o.UserId == ownerId, ct) ? oid : null;

        long? productId = null;
        if (req.ProductId is { } pid)
            productId = await db.Products.AnyAsync(p => p.ProductId == pid, ct) ? pid : null;

        var now = DateTime.UtcNow;
        var convo = new SupportTicket
        {
            Axis = ConversationAxis.ShopperMerchant,
            Subject = subject,
            Status = "Open",
            Priority = "Normal",
            ShopperUserId = shopperUserId,
            ShopperEmail = email,
            OrderId = orderId,
            ProductId = productId,
            LastMessageAt = now,
            CreatedAt = now,
        };
        db.SupportTickets.Add(convo);
        await db.SaveChangesAsync(ct);

        convo.Reference = SupportService.BuildReference(now, convo.SupportTicketId);
        db.SupportMessages.Add(new SupportMessage
        {
            SupportTicketId = convo.SupportTicketId,
            AuthorUserId = shopperUserId,
            AuthorType = MessageAuthorType.Shopper,
            Body = body,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);

        var who = Trim(req.Name, 120) is { Length: > 0 } n ? n : email;
        await feed.NotifyAdminsAsync("Conversation", $"New message from {who}", subject, $"/admin/inbox/{convo.SupportTicketId}", ct);

        return await BuildThreadAsync(convo, includeToken: true, ct);
    }

    public async Task<IReadOnlyList<ConversationDto>> MineAsync(long shopperUserId, CancellationToken ct = default) =>
        (await Shopper().Where(c => c.ShopperUserId == shopperUserId)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).ToListAsync(ct))
            .Select(Map).ToList();

    public async Task<ConversationThreadDto> GetForShopperAsync(long id, long shopperUserId, CancellationToken ct = default)
    {
        var convo = await Shopper().FirstOrDefaultAsync(c => c.SupportTicketId == id && c.ShopperUserId == shopperUserId, ct)
                    ?? throw new AppException("Conversation not found.", StatusCodes.Status404NotFound);
        return await BuildThreadAsync(convo, includeToken: false, ct);
    }

    public async Task<ConversationThreadDto> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        var id = ResolveToken(token);
        var convo = await Shopper().FirstOrDefaultAsync(c => c.SupportTicketId == id, ct)
                    ?? throw new AppException("Conversation not found.", StatusCodes.Status404NotFound);
        return await BuildThreadAsync(convo, includeToken: true, ct);
    }

    public async Task ReplyAsShopperAsync(long id, string body, long? shopperUserId, string? token, CancellationToken ct = default)
    {
        body = Trim(body, 4000);
        if (body.Length == 0) throw new AppException("Message is empty.", StatusCodes.Status400BadRequest);

        // Either the signed token names this conversation, or the signed-in shopper owns it.
        if (token is { Length: > 0 })
        {
            if (ResolveToken(token) != id) throw new AppException("Conversation not found.", StatusCodes.Status404NotFound);
        }
        else if (shopperUserId is null)
        {
            throw new AppException("Sign in to reply.", StatusCodes.Status401Unauthorized);
        }

        var convo = await db.SupportTickets
            .Where(c => c.Axis == ConversationAxis.ShopperMerchant)
            .FirstOrDefaultAsync(c => c.SupportTicketId == id, ct)
            ?? throw new AppException("Conversation not found.", StatusCodes.Status404NotFound);

        if (token is null or "" && convo.ShopperUserId != shopperUserId)
            throw new AppException("Conversation not found.", StatusCodes.Status404NotFound);

        var now = DateTime.UtcNow;
        db.SupportMessages.Add(new SupportMessage
        {
            SupportTicketId = id, AuthorUserId = shopperUserId,
            AuthorType = MessageAuthorType.Shopper, Body = body, CreatedAt = now,
        });
        convo.LastMessageAt = now;
        if (convo.Status is "Closed" or "Pending") { convo.Status = "Open"; convo.ResolvedAt = null; }
        convo.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        await feed.NotifyAdminsAsync("Conversation", $"Reply on {convo.Reference ?? "a conversation"}",
            Truncate(body, 120), $"/admin/inbox/{id}", ct);
    }

    // ---------------- Merchant side ----------------

    public async Task<PagedResult<ConversationDto>> InboxAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = Shopper();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(c => c.Status == status);

        var total = await q.LongCountAsync(ct);
        var rows = await q.OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<ConversationDto>
        {
            Items = rows.Select(Map).ToList(), Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    public async Task<ConversationThreadDto> GetForMerchantAsync(long id, CancellationToken ct = default)
    {
        var convo = await Shopper().FirstOrDefaultAsync(c => c.SupportTicketId == id, ct)
                    ?? throw new AppException("Conversation not found.", StatusCodes.Status404NotFound);
        return await BuildThreadAsync(convo, includeToken: false, ct);
    }

    public async Task ReplyAsMerchantAsync(long id, string body, long merchantUserId, CancellationToken ct = default)
    {
        body = Trim(body, 4000);
        if (body.Length == 0) throw new AppException("Message is empty.", StatusCodes.Status400BadRequest);

        var convo = await db.SupportTickets
            .Where(c => c.Axis == ConversationAxis.ShopperMerchant)
            .FirstOrDefaultAsync(c => c.SupportTicketId == id, ct)
            ?? throw new AppException("Conversation not found.", StatusCodes.Status404NotFound);

        var now = DateTime.UtcNow;
        db.SupportMessages.Add(new SupportMessage
        {
            SupportTicketId = id, AuthorUserId = merchantUserId,
            AuthorType = MessageAuthorType.Merchant, Body = body, CreatedAt = now,
        });
        convo.LastMessageAt = now;
        convo.FirstResponseAt ??= now;
        if (convo.Status == "Open") convo.Status = "Pending";   // awaiting the shopper
        convo.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        if (convo.ShopperUserId is { } uid)
            await feed.NotifyUserAsync(uid, "Conversation", "Reply from the store", Truncate(body, 120), "/account/conversations", ct);
    }

    public async Task SetStatusAsync(long id, string status, CancellationToken ct = default)
    {
        if (!Statuses.Contains(status)) throw new AppException("Invalid status.", StatusCodes.Status400BadRequest);
        var convo = await db.SupportTickets
            .Where(c => c.Axis == ConversationAxis.ShopperMerchant)
            .FirstOrDefaultAsync(c => c.SupportTicketId == id, ct)
            ?? throw new AppException("Conversation not found.", StatusCodes.Status404NotFound);

        convo.Status = status;
        convo.ResolvedAt = string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null;
        convo.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<int> OpenCountAsync(CancellationToken ct = default) =>
        db.SupportTickets.CountAsync(c => c.Axis == ConversationAxis.ShopperMerchant && c.Status == "Open", ct);

    // ---------------- helpers ----------------

    private IQueryable<SupportTicket> Shopper() =>
        db.SupportTickets.AsNoTracking().Where(c => c.Axis == ConversationAxis.ShopperMerchant);

    private async Task<ConversationThreadDto> BuildThreadAsync(SupportTicket convo, bool includeToken, CancellationToken ct)
    {
        var messages = await db.SupportMessages.AsNoTracking()
            .Where(m => m.SupportTicketId == convo.SupportTicketId && !m.IsInternalNote)
            .OrderBy(m => m.SupportMessageId)
            .Select(m => new ConversationMessageDto(m.SupportMessageId, m.AuthorType, m.Body, m.CreatedAt))
            .ToListAsync(ct);

        var token = includeToken ? Protector.Protect(convo.SupportTicketId.ToString()) : null;
        return new ConversationThreadDto(Map(convo), messages, token);
    }

    private long ResolveToken(string token)
    {
        try { return long.Parse(Protector.Unprotect(token)); }
        catch { throw new AppException("That link is no longer valid.", StatusCodes.Status404NotFound); }
    }

    private static ConversationDto Map(SupportTicket c) =>
        new(c.SupportTicketId, c.Reference, c.Subject, c.Status, c.ShopperEmail,
            c.OrderId, c.ProductId, c.CreatedAt, c.LastMessageAt, c.FirstResponseAt);

    private static string Trim(string? v, int max)
    {
        var t = (v ?? string.Empty).Trim();
        return t.Length <= max ? t : t[..max];
    }

    private static string Truncate(string v, int max) => v.Length <= max ? v : v[..max] + "…";
}
