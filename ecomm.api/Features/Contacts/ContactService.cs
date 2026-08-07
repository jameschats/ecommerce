using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Contacts;

public sealed record SubmitContactRequest(
    string Name, string? Email, string? Phone, string? Subject, string? Message,
    string? SourcePage,
    /// <summary>Honeypot. Real people never fill this in; bots fill in everything.</summary>
    string? Website = null,
    bool SubscribeToEmails = false);

public sealed record ContactDto(
    long ContactId, string Name, string? Email, string? Phone, string? Subject, string? Message,
    string Source, string? SourcePage, string Status, string? AdminNotes,
    bool SubscribedToEmails, DateTime CreatedAt);

/// <summary>
/// Every field is optional so the inbox can keep patching just status or notes, while the
/// edit form sends the whole record. A null means "leave alone", not "clear".
/// </summary>
public sealed record UpdateContactRequest(
    string? Status, string? AdminNotes, bool? SubscribedToEmails,
    string? Name = null, string? Email = null, string? Phone = null,
    string? Subject = null, string? Message = null);

/// <summary>An enquiry taken by hand — over the phone, at the counter, or from a card.</summary>
public sealed record CreateContactRequest(
    string Name, string? Email, string? Phone, string? Subject, string? Message,
    bool SubscribeToEmails = false);

public sealed record ContactQuery(string? Status = null, string? Search = null, int Page = 1, int PageSize = 25);

public interface IContactService
{
    Task<long> SubmitAsync(SubmitContactRequest req, long? userId, CancellationToken ct = default);
    Task<PagedResult<ContactDto>> ListAsync(ContactQuery query, CancellationToken ct = default);
    Task<ContactDto?> UpdateAsync(long id, UpdateContactRequest req, CancellationToken ct = default);
    Task<ContactDto> CreateAsync(CreateContactRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    Task<int> NewCountAsync(CancellationToken ct = default);
}

/// <summary>
/// Visitor enquiries (Anna's "contacts management").
///
/// The contact page used to throw submissions away — its own template said so. Everything
/// here exists so that stops being true, and so the same list can later be a campaign
/// audience without a second capture mechanism.
/// </summary>
public sealed class ContactService : IContactService
{
    private const long Tenant = 1;

    /// <summary>
    /// How long one sender must wait before writing in again. Long enough to stop a script
    /// filling the inbox, short enough that a person who forgot to mention something is not
    /// locked out.
    /// </summary>
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromMinutes(2);

    private readonly EcommerceDbContext _db;
    private readonly Notifications.INotificationFeedService _feed;

    public ContactService(EcommerceDbContext db, Notifications.INotificationFeedService feed)
    {
        _db = db;
        _feed = feed;
    }

    public async Task<long> SubmitAsync(SubmitContactRequest req, long? userId, CancellationToken ct = default)
    {
        // Honeypot: the field is hidden from people, so anything in it came from a bot.
        // Accepted silently rather than rejected — telling a bot it failed teaches it to
        // try again differently.
        if (!string.IsNullOrWhiteSpace(req.Website)) return 0;

        var name = req.Name?.Trim() ?? string.Empty;
        var email = req.Email?.Trim();
        var phone = req.Phone?.Trim();
        var message = req.Message?.Trim();

        if (name.Length == 0) throw new AppException("Please tell us your name.");
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            throw new AppException("Leave an email address or a phone number so we can reply.");
        if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@'))
            throw new AppException("That email address does not look right.");
        if (string.IsNullOrWhiteSpace(message)) throw new AppException("Please write your message.");

        // One submission per sender per window. Keyed on whatever they gave us, so it works
        // for anonymous senders who have no account to rate-limit against.
        var since = DateTime.UtcNow - RepeatWindow;
        var recent = await _db.Contacts.AnyAsync(
            c => c.TenantId == Tenant && c.CreatedAt >= since
                 && ((email != null && c.Email == email) || (phone != null && c.Phone == phone)), ct);
        if (recent)
            throw new AppException(
                "Thanks — we already have your message and will reply shortly.",
                StatusCodes.Status429TooManyRequests);

        var contact = new Contact
        {
            TenantId = Tenant,
            Name = name,
            Email = email,
            Phone = phone,
            Subject = req.Subject?.Trim(),
            Message = message,
            Source = "ContactForm",
            SourcePage = req.SourcePage?.Trim(),
            Status = "New",
            UserId = userId,
            SubscribedToEmails = req.SubscribeToEmails,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(ct);

        // An enquiry nobody is told about is barely better than one that was thrown away.
        await _feed.NotifyAdminsAsync(
            "Contact", $"New enquiry from {name}",
            contact.Subject is { Length: > 0 } s ? s : Truncate(message, 80),
            "/admin/contacts", ct);

        return contact.ContactId;
    }

    public async Task<PagedResult<ContactDto>> ListAsync(ContactQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = _db.Contacts.AsNoTracking().Where(c => c.TenantId == Tenant);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(c => c.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(c => c.Name.Contains(s) || (c.Email != null && c.Email.Contains(s))
                             || (c.Phone != null && c.Phone.Contains(s))
                             || (c.Subject != null && c.Subject.Contains(s))
                             || (c.Message != null && c.Message.Contains(s)));
        }

        var total = await q.LongCountAsync(ct);
        var items = await q
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new ContactDto(
                c.ContactId, c.Name, c.Email, c.Phone, c.Subject, c.Message,
                c.Source, c.SourcePage, c.Status, c.AdminNotes, c.SubscribedToEmails, c.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<ContactDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<ContactDto?> UpdateAsync(long id, UpdateContactRequest req, CancellationToken ct = default)
    {
        var c = await _db.Contacts.FirstOrDefaultAsync(x => x.ContactId == id && x.TenantId == Tenant, ct);
        if (c is null) return null;

        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            var status = req.Status.Trim();
            if (status is not ("New" or "Open" or "Closed" or "Spam"))
                throw new AppException("Status must be New, Open, Closed or Spam.");
            c.Status = status;
        }

        if (req.AdminNotes is not null) c.AdminNotes = req.AdminNotes.Trim();
        if (req.SubscribedToEmails is { } sub) c.SubscribedToEmails = sub;

        // Details are only touched when sent, so the inbox's status-and-notes patch cannot
        // blank out a name and number somebody wrote down.
        if (req.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Name is required.");
            c.Name = req.Name.Trim();
        }
        if (req.Email is not null) c.Email = Blank(req.Email);
        if (req.Phone is not null) c.Phone = Blank(req.Phone);
        if (req.Subject is not null) c.Subject = Blank(req.Subject);
        if (req.Message is not null) c.Message = Blank(req.Message);

        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Dto(c);
    }

    /// <summary>
    /// An enquiry added by hand. Source is "Admin" rather than "ContactForm" so the inbox
    /// still says where each row came from, and the honeypot and repeat-window checks that
    /// guard the public form are skipped — they exist to stop bots, and this is a person
    /// typing in the back office.
    /// </summary>
    public async Task<ContactDto> CreateAsync(CreateContactRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Name is required.");
        if (string.IsNullOrWhiteSpace(req.Email) && string.IsNullOrWhiteSpace(req.Phone))
            throw new AppException("Give an email or a phone number — otherwise there is no way to reply.");

        var c = new Contact
        {
            TenantId = Tenant,
            Name = req.Name.Trim(),
            Email = Blank(req.Email),
            Phone = Blank(req.Phone),
            Subject = Blank(req.Subject),
            Message = Blank(req.Message),
            Source = "Admin",
            Status = "New",
            SubscribedToEmails = req.SubscribeToEmails,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Contacts.Add(c);
        await _db.SaveChangesAsync(ct);
        return Dto(c);
    }

    /// <summary>
    /// Removed outright. An enquiry is a message, not a financial record, and campaigns match
    /// their recipients by email address rather than by contact id — so nothing is orphaned.
    /// </summary>
    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var c = await _db.Contacts.FirstOrDefaultAsync(x => x.ContactId == id && x.TenantId == Tenant, ct);
        if (c is null) return false;

        _db.Contacts.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static ContactDto Dto(Contact c) =>
        new(c.ContactId, c.Name, c.Email, c.Phone, c.Subject, c.Message,
            c.Source, c.SourcePage, c.Status, c.AdminNotes, c.SubscribedToEmails, c.CreatedAt);

    public Task<int> NewCountAsync(CancellationToken ct = default) =>
        _db.Contacts.CountAsync(c => c.TenantId == Tenant && c.Status == "New", ct);

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
}
