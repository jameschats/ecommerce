using System.Text;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Customers;

public sealed record CustomerListItem(
    long UserId, string? FullName, string? Email, string? PhoneNumber,
    int OrderCount, decimal TotalSpent, DateTime? LastOrderAt, bool AcceptsEmailMarketing,
    IReadOnlyList<string> Tags, DateTime CreatedAt);

public sealed record TagCountDto(string Tag, int Count);
public sealed record CustomerImportResult(int Total, int Created, int Updated, int Skipped, List<string> Errors);

public sealed record CustomerAddressDto(
    long CustomerAddressId, string? Label, string? RecipientName, string? Phone,
    string Line1, string? Line2, string City, string State, string Pincode, string Country, bool IsDefault);

public sealed record CustomerOrderDto(long OrderId, string OrderNumber, string Status, decimal TotalAmount, DateTime? PlacedAt);

public sealed record CustomerDetailDto(
    long UserId, string? FullName, string? Email, string? PhoneNumber, bool IsActive, bool IsEmailVerified, DateTime CreatedAt,
    bool AcceptsEmailMarketing, bool AcceptsSmsMarketing, bool AcceptsWhatsappMarketing, string? Notes, IReadOnlyList<string> Tags,
    int OrderCount, decimal TotalSpent, DateTime? LastOrderAt,
    IReadOnlyList<CustomerAddressDto> Addresses, IReadOnlyList<CustomerOrderDto> RecentOrders);

public sealed record CreateCustomerRequest(string? FullName, string? Email, string? PhoneNumber,
    bool AcceptsEmailMarketing, bool AcceptsSmsMarketing, bool AcceptsWhatsappMarketing, string? Notes, string? Tags);

public sealed record UpdateCustomerRequest(string? FullName, string? PhoneNumber,
    bool AcceptsEmailMarketing, bool AcceptsSmsMarketing, bool AcceptsWhatsappMarketing, string? Notes, string? Tags);

public sealed record SegmentDto(string Key, string Label, int Count);

/// <summary>A consented email recipient for a marketing campaign send (M1).</summary>
public sealed record CampaignRecipient(long UserId, string Email, string? FullName);

public interface ICustomerAdminService
{
    /// <summary>Customers in a segment who have opted in to email marketing (consent enforced regardless of segment).</summary>
    Task<IReadOnlyList<CampaignRecipient>> EmailRecipientsAsync(string? segment, CancellationToken ct = default);
    Task<PagedResult<CustomerListItem>> ListAsync(string? search, string? segment, string? tag, int page, int pageSize, CancellationToken ct = default);
    Task<CustomerDetailDto> GetAsync(long userId, CancellationToken ct = default);
    Task<CustomerDetailDto> CreateAsync(CreateCustomerRequest req, CancellationToken ct = default);
    Task<CustomerDetailDto> UpdateAsync(long userId, UpdateCustomerRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<SegmentDto>> SegmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TagCountDto>> TagsAsync(CancellationToken ct = default);
    Task<CustomerImportResult> ImportAsync(Stream csv, CancellationToken ct = default);
}

/// <summary>
/// Merchant-admin Customers: shoppers are <see cref="User"/>s in the CUSTOMER role. Order aggregates
/// (count / spend / last order) reuse the same "sold" statuses as analytics. CRM fields (consent, notes,
/// tags) live on <see cref="CustomerProfile"/>. All queries are tenant-scoped by the global query filters.
/// </summary>
public sealed class CustomerAdminService(EcommerceDbContext db) : ICustomerAdminService
{
    private const string CustomerRole = "CUSTOMER";
    private static readonly string[] SoldStatuses = { "Paid", "Confirmed", "Packed", "Shipped", "Delivered" };

    public async Task<PagedResult<CustomerListItem>> ListAsync(string? search, string? segment, string? tag, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var all = await LoadAllAsync(ct);

        IEnumerable<CustomerListItem> q = all;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c =>
                (c.FullName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || (c.Email?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || (c.PhoneNumber?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        q = ApplySegment(q, segment);
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var tg = tag.Trim();
            q = q.Where(c => c.Tags.Any(t => string.Equals(t, tg, StringComparison.OrdinalIgnoreCase)));
        }

        var ordered = q.OrderByDescending(c => c.LastOrderAt ?? DateTime.MinValue).ThenByDescending(c => c.CreatedAt).ToList();
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<CustomerListItem> { Items = pageItems, Page = page, PageSize = pageSize, TotalCount = ordered.Count };
    }

    public async Task<IReadOnlyList<SegmentDto>> SegmentsAsync(CancellationToken ct = default)
    {
        var all = await LoadAllAsync(ct);
        return new List<SegmentDto>
        {
            new("all", "All customers", all.Count),
            new("paid", "Purchased at least once", all.Count(c => c.OrderCount >= 1)),
            new("repeat", "Repeat customers", all.Count(c => c.OrderCount >= 2)),
            new("prospect", "Not purchased yet", all.Count(c => c.OrderCount == 0)),
            new("subscribers", "Email subscribers", all.Count(c => c.AcceptsEmailMarketing)),
        };
    }

    public async Task<CustomerDetailDto> GetAsync(long userId, CancellationToken ct = default)
    {
        var user = await FindCustomerAsync(userId, ct) ?? throw NotFound();
        var profile = await db.CustomerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var addresses = await db.CustomerAddresses.Where(a => a.UserId == userId && !a.IsDeleted)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.CustomerAddressId)
            .Select(a => new CustomerAddressDto(a.CustomerAddressId, a.Label, a.RecipientName, a.Phone,
                a.Line1, a.Line2, a.City, a.State, a.Pincode, a.Country, a.IsDefault))
            .ToListAsync(ct);

        var orders = await db.Orders.Where(o => o.UserId == userId)
            .OrderByDescending(o => o.PlacedAt ?? o.CreatedAt).Take(20)
            .Select(o => new CustomerOrderDto(o.OrderId, o.OrderNumber, o.Status, o.TotalAmount, o.PlacedAt))
            .ToListAsync(ct);

        var sold = orders.Where(o => SoldStatuses.Contains(o.Status) && o.PlacedAt != null).ToList();
        // Aggregate over ALL sold orders (not just the recent 20) for accurate lifetime value.
        var lifetime = await db.Orders.Where(o => o.UserId == userId && SoldStatuses.Contains(o.Status) && o.PlacedAt != null)
            .GroupBy(o => o.UserId)
            .Select(g => new { Count = g.Count(), Spent = g.Sum(x => x.TotalAmount), Last = g.Max(x => x.PlacedAt) })
            .FirstOrDefaultAsync(ct);

        return ToDetail(user, profile, addresses, orders,
            lifetime?.Count ?? 0, lifetime?.Spent ?? 0m, lifetime?.Last);
    }

    public async Task<CustomerDetailDto> CreateAsync(CreateCustomerRequest req, CancellationToken ct = default)
    {
        var email = req.Email?.Trim();
        var phone = req.PhoneNumber?.Trim();
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            throw new AppException("A customer needs at least an email or a phone number.");

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.ToUpperInvariant();
            if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized && !u.IsDeleted, ct))
                throw new AppException("A customer with that email already exists.", StatusCodes.Status409Conflict);
        }

        var user = new User
        {
            Email = email,
            NormalizedEmail = email?.ToUpperInvariant(),
            FullName = req.FullName?.Trim(),
            PhoneNumber = phone,
            PasswordHash = null,   // admin-created; customer can set a password later via reset/OTP
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);   // TenantId auto-stamped; UserId assigned

        var role = await db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == CustomerRole, ct);
        if (role is not null && !await db.UserRoles.AnyAsync(ur => ur.UserId == user.UserId && ur.RoleId == role.RoleId, ct))
            db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId });

        db.CustomerProfiles.Add(new CustomerProfile
        {
            UserId = user.UserId,
            AcceptsEmailMarketing = req.AcceptsEmailMarketing,
            AcceptsSmsMarketing = req.AcceptsSmsMarketing,
            AcceptsWhatsappMarketing = req.AcceptsWhatsappMarketing,
            Notes = req.Notes?.Trim(),
            Tags = NormalizeTags(req.Tags),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return await GetAsync(user.UserId, ct);
    }

    public async Task<CustomerDetailDto> UpdateAsync(long userId, UpdateCustomerRequest req, CancellationToken ct = default)
    {
        var user = await FindCustomerAsync(userId, ct) ?? throw NotFound();
        user.FullName = req.FullName?.Trim();
        user.PhoneNumber = req.PhoneNumber?.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        var profile = await db.CustomerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            profile = new CustomerProfile { UserId = userId, CreatedAt = DateTime.UtcNow };
            db.CustomerProfiles.Add(profile);
        }
        profile.AcceptsEmailMarketing = req.AcceptsEmailMarketing;
        profile.AcceptsSmsMarketing = req.AcceptsSmsMarketing;
        profile.AcceptsWhatsappMarketing = req.AcceptsWhatsappMarketing;
        profile.Notes = req.Notes?.Trim();
        profile.Tags = NormalizeTags(req.Tags);
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    public async Task<IReadOnlyList<TagCountDto>> TagsAsync(CancellationToken ct = default)
    {
        var all = await LoadAllAsync(ct);
        return all.SelectMany(c => c.Tags)
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TagCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Tag)
            .ToList();
    }

    /// <summary>
    /// Bulk-import customers from a CSV (headers: name, email, phone, tags, notes, and optional
    /// email/sms/whatsapp marketing). Matches existing shoppers by email — updates them (tags are
    /// merged, not wiped); otherwise creates a CUSTOMER user + profile. Rows with neither email
    /// nor phone are skipped with a reason.
    /// </summary>
    public async Task<CustomerImportResult> ImportAsync(Stream csv, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csv);
        var rows = ParseCsv(await reader.ReadToEndAsync(ct));
        if (rows.Count < 2) throw new AppException("The file has no data rows.");

        var headers = rows[0].Select(h => (h ?? "").Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names) => headers.FindIndex(h => names.Contains(h));
        int iName = Col("name", "full name", "fullname", "customer"),
            iEmail = Col("email", "email address"),
            iPhone = Col("phone", "phone number", "phonenumber", "mobile"),
            iTags = Col("tags", "tag"),
            iNotes = Col("notes", "note"),
            iEmailMk = Col("email marketing", "acceptsemailmarketing", "subscribed"),
            iSmsMk = Col("sms marketing", "acceptssmsmarketing"),
            iWaMk = Col("whatsapp marketing", "acceptswhatsappmarketing");
        if (iEmail < 0 && iPhone < 0)
            throw new AppException("The file needs at least an 'email' or 'phone' column.");

        var role = await db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == CustomerRole, ct);
        string? Cell(string[] r, int i) => i >= 0 && i < r.Length ? r[i]?.Trim() : null;

        int total = 0, created = 0, updated = 0, skipped = 0;
        var errors = new List<string>();

        for (var n = 1; n < rows.Count; n++)
        {
            var r = rows[n];
            if (r.All(string.IsNullOrWhiteSpace)) continue;
            total++;
            var email = Cell(r, iEmail);
            var phone = Cell(r, iPhone);
            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            {
                skipped++; errors.Add($"Row {n + 1}: no email or phone."); continue;
            }
            if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@'))
            {
                skipped++; errors.Add($"Row {n + 1}: invalid email '{email}'."); continue;
            }

            var name = Cell(r, iName);
            var tags = NormalizeTags(Cell(r, iTags));
            var notes = Cell(r, iNotes);
            bool wantEmail = Truthy(Cell(r, iEmailMk)), wantSms = Truthy(Cell(r, iSmsMk)), wantWa = Truthy(Cell(r, iWaMk));

            User? user = null;
            if (!string.IsNullOrWhiteSpace(email))
            {
                var norm = email.ToUpperInvariant();
                user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == norm && !u.IsDeleted, ct);
            }

            if (user is null)
            {
                user = new User
                {
                    Email = email, NormalizedEmail = email?.ToUpperInvariant(), FullName = name, PhoneNumber = phone,
                    PasswordHash = null, IsActive = true, CreatedAt = DateTime.UtcNow,
                };
                db.Users.Add(user);
                await db.SaveChangesAsync(ct);   // assign UserId + stamp tenant
                if (role is not null) db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId });
                db.CustomerProfiles.Add(new CustomerProfile
                {
                    UserId = user.UserId, AcceptsEmailMarketing = wantEmail, AcceptsSmsMarketing = wantSms,
                    AcceptsWhatsappMarketing = wantWa, Notes = notes, Tags = tags, CreatedAt = DateTime.UtcNow,
                });
                created++;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(name)) user.FullName = name;
                if (!string.IsNullOrWhiteSpace(phone)) user.PhoneNumber = phone;
                user.UpdatedAt = DateTime.UtcNow;

                var profile = await db.CustomerProfiles.FirstOrDefaultAsync(p => p.UserId == user.UserId, ct);
                if (profile is null)
                {
                    profile = new CustomerProfile { UserId = user.UserId, CreatedAt = DateTime.UtcNow };
                    db.CustomerProfiles.Add(profile);
                }
                profile.Tags = MergeTags(profile.Tags, tags);   // add, don't wipe
                if (!string.IsNullOrWhiteSpace(notes)) profile.Notes = notes;
                if (wantEmail) profile.AcceptsEmailMarketing = true;
                if (wantSms) profile.AcceptsSmsMarketing = true;
                if (wantWa) profile.AcceptsWhatsappMarketing = true;
                profile.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
            await db.SaveChangesAsync(ct);
        }

        if (errors.Count > 50) errors = errors.Take(50).Append($"…and {errors.Count - 50} more.").ToList();
        return new CustomerImportResult(total, created, updated, skipped, errors);
    }

    // ---- helpers ----
    private async Task<List<CustomerListItem>> LoadAllAsync(CancellationToken ct)
    {
        var customers = await (from u in db.Users
                               join ur in db.UserRoles on u.UserId equals ur.UserId
                               join r in db.Roles on ur.RoleId equals r.RoleId
                               where !u.IsDeleted && r.NormalizedName == CustomerRole
                               select new { u.UserId, u.FullName, u.Email, u.PhoneNumber, u.CreatedAt }).ToListAsync(ct);

        var agg = (await db.Orders.Where(o => SoldStatuses.Contains(o.Status) && o.PlacedAt != null)
                .GroupBy(o => o.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), Spent = g.Sum(x => x.TotalAmount), Last = g.Max(x => x.PlacedAt) })
                .ToListAsync(ct))
            .ToDictionary(x => x.UserId);

        var profiles = (await db.CustomerProfiles.Select(p => new { p.UserId, p.AcceptsEmailMarketing, p.Tags }).ToListAsync(ct))
            .ToDictionary(p => p.UserId);

        return customers.Select(c =>
        {
            var a = agg.GetValueOrDefault(c.UserId);
            var p = profiles.GetValueOrDefault(c.UserId);
            return new CustomerListItem(c.UserId, c.FullName, c.Email, c.PhoneNumber,
                a?.Count ?? 0, a?.Spent ?? 0m, a?.Last, p?.AcceptsEmailMarketing ?? false, SplitTags(p?.Tags), c.CreatedAt);
        }).ToList();
    }

    public async Task<IReadOnlyList<CampaignRecipient>> EmailRecipientsAsync(string? segment, CancellationToken ct = default)
    {
        var all = await LoadAllAsync(ct);
        // Marketing consent is enforced on top of the segment — a shopper who hasn't opted in to email
        // marketing is never included, even in the "all" segment. This is a legal boundary, not a filter.
        return ApplySegment(all, segment)
            .Where(c => c.AcceptsEmailMarketing && !string.IsNullOrWhiteSpace(c.Email))
            .Select(c => new CampaignRecipient(c.UserId, c.Email!, c.FullName))
            .ToList();
    }

    private static IEnumerable<CustomerListItem> ApplySegment(IEnumerable<CustomerListItem> q, string? segment) => segment switch
    {
        "paid" => q.Where(c => c.OrderCount >= 1),
        "repeat" => q.Where(c => c.OrderCount >= 2),
        "prospect" => q.Where(c => c.OrderCount == 0),
        "subscribers" => q.Where(c => c.AcceptsEmailMarketing),
        _ => q,
    };

    private Task<User?> FindCustomerAsync(long userId, CancellationToken ct) =>
        (from u in db.Users
         join ur in db.UserRoles on u.UserId equals ur.UserId
         join r in db.Roles on ur.RoleId equals r.RoleId
         where u.UserId == userId && !u.IsDeleted && r.NormalizedName == CustomerRole
         select u).FirstOrDefaultAsync(ct);

    private static CustomerDetailDto ToDetail(User u, CustomerProfile? p, IReadOnlyList<CustomerAddressDto> addresses,
        IReadOnlyList<CustomerOrderDto> orders, int orderCount, decimal totalSpent, DateTime? lastOrderAt) =>
        new(u.UserId, u.FullName, u.Email, u.PhoneNumber, u.IsActive, u.IsEmailVerified, u.CreatedAt,
            p?.AcceptsEmailMarketing ?? false, p?.AcceptsSmsMarketing ?? false, p?.AcceptsWhatsappMarketing ?? false,
            p?.Notes, SplitTags(p?.Tags), orderCount, totalSpent, lastOrderAt, addresses, orders);

    private static string? NormalizeTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags) ? null
        : string.Join(",", tags.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct());

    private static string? MergeTags(string? existing, string? incoming)
    {
        var merged = new List<string>();
        merged.AddRange(SplitTags(existing));
        merged.AddRange(SplitTags(incoming));
        var distinct = merged.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return distinct.Count == 0 ? null : string.Join(",", distinct);
    }

    private static bool Truthy(string? v) =>
        v is not null && v.Trim().ToLowerInvariant() is "yes" or "true" or "1" or "y";

    // Minimal RFC-4180-ish CSV reader (quotes, escaped "" , embedded commas/newlines).
    private static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var field = new StringBuilder();
        var record = new List<string>();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(ch);
            }
            else
            {
                switch (ch)
                {
                    case '"': inQuotes = true; break;
                    case ',': record.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n': record.Add(field.ToString()); field.Clear(); rows.Add(record.ToArray()); record = []; break;
                    default: field.Append(ch); break;
                }
            }
        }
        if (field.Length > 0 || record.Count > 0) { record.Add(field.ToString()); rows.Add(record.ToArray()); }
        return rows;
    }

    private static IReadOnlyList<string> SplitTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags) ? []
        : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static AppException NotFound() => new("Customer not found.", StatusCodes.Status404NotFound);
}
