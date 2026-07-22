using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Contact;

/// <summary><paramref name="Website"/> is a honeypot — real users never fill it, bots usually do.</summary>
public sealed record SubmitContactRequest(
    string Name, string Email, string? Phone, string? Subject, string Body, string? SourceUrl, string? Website);

public sealed record ContactMessageDto(
    long ContactMessageId, string Name, string Email, string? Phone, string? Subject, string Body,
    string? SourceUrl, string Status, DateTime CreatedAt, DateTime? HandledAt);

public interface IContactService
{
    Task SubmitAsync(SubmitContactRequest req, CancellationToken ct = default);
    Task<PagedResult<ContactMessageDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<ContactMessageDto> SetStatusAsync(long id, string status, long? userId, CancellationToken ct = default);
    Task<int> NewCountAsync(CancellationToken ct = default);
}

/// <summary>
/// Storefront contact form. Replaces a form that previously discarded every submission.
/// Anonymous input, so: length caps, a honeypot, and IP rate limiting at the controller.
/// A submission raises an admin bell notification — the merchant should not have to go
/// looking for enquiries.
/// </summary>
public sealed class ContactService(EcommerceDbContext db, INotificationFeedService feed) : IContactService
{
    public async Task SubmitAsync(SubmitContactRequest req, CancellationToken ct = default)
    {
        // Honeypot: silently accept so a bot can't distinguish success from rejection.
        if (!string.IsNullOrWhiteSpace(req.Website)) return;

        var name = Clean(req.Name, 120);
        var email = Clean(req.Email, 200);
        var body = Clean(req.Body, 4000);

        if (name.Length == 0 || email.Length == 0 || body.Length == 0)
            throw new AppException("Please fill in your name, email and message.");
        if (!email.Contains('@') || !email.Contains('.'))
            throw new AppException("That email address doesn't look right.");

        var msg = new ContactMessage
        {
            Name = name,
            Email = email,
            Phone = Clean(req.Phone, 30) is { Length: > 0 } p ? p : null,
            Subject = Clean(req.Subject, 200) is { Length: > 0 } s ? s : null,
            Body = body,
            SourceUrl = Clean(req.SourceUrl, 500) is { Length: > 0 } u ? u : null,
            Status = "New",
            CreatedAt = DateTime.UtcNow,
        };
        db.ContactMessages.Add(msg);
        await db.SaveChangesAsync(ct);

        await feed.NotifyAdminsAsync(
            "ContactMessage",
            $"New message from {msg.Name}",
            msg.Subject ?? Truncate(msg.Body, 120),
            "/admin/messages", ct);
    }

    public async Task<PagedResult<ContactMessageDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = db.ContactMessages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(m => m.Status == status);

        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(m => m.ContactMessageId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new ContactMessageDto(
                m.ContactMessageId, m.Name, m.Email, m.Phone, m.Subject, m.Body,
                m.SourceUrl, m.Status, m.CreatedAt, m.HandledAt))
            .ToListAsync(ct);

        return new PagedResult<ContactMessageDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<ContactMessageDto> SetStatusAsync(long id, string status, long? userId, CancellationToken ct = default)
    {
        if (status is not ("New" or "Handled")) throw new AppException("Unknown status.");

        var m = await db.ContactMessages.FirstOrDefaultAsync(x => x.ContactMessageId == id, ct)
            ?? throw new AppException("Message not found.", 404);

        m.Status = status;
        m.HandledByUserId = status == "Handled" ? userId : null;
        m.HandledAt = status == "Handled" ? DateTime.UtcNow : null;
        await db.SaveChangesAsync(ct);

        return new ContactMessageDto(m.ContactMessageId, m.Name, m.Email, m.Phone, m.Subject, m.Body,
            m.SourceUrl, m.Status, m.CreatedAt, m.HandledAt);
    }

    public Task<int> NewCountAsync(CancellationToken ct = default) =>
        db.ContactMessages.CountAsync(m => m.Status == "New", ct);

    private static string Clean(string? value, int max)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
