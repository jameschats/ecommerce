using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Campaigns;

public sealed record CampaignDto(
    long CampaignId, string Name, string Subject, string Body, string Audience, string Status,
    int TotalRecipients, int SentCount, int FailedCount,
    DateTime? StartedAt, DateTime? CompletedAt, DateTime CreatedAt);

public sealed record SaveCampaignRequest(string Name, string Subject, string Body, string Audience);
public sealed record AudienceCountDto(int Contacts, int Customers, int Both);
public sealed record SendResultDto(int Attempted, int Sent, int Failed, int Remaining, string Status);

public interface ICampaignService
{
    Task<List<CampaignDto>> ListAsync(CancellationToken ct = default);
    Task<CampaignDto> CreateAsync(SaveCampaignRequest req, CancellationToken ct = default);
    Task<CampaignDto?> UpdateAsync(long id, SaveCampaignRequest req, CancellationToken ct = default);
    Task<AudienceCountDto> AudienceCountsAsync(CancellationToken ct = default);
    Task<SendResultDto> SendBatchAsync(long id, int batchSize, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Promotional email campaigns.
///
/// Sends in batches driven by the admin screen rather than through a background worker.
/// The only IHostedService here is the admin seeder, and a scheduler for a shop that mails
/// occasionally would be machinery to maintain for no gain. Resumability comes from a row
/// per recipient instead: a send that stops halfway knows exactly who is left.
/// </summary>
public sealed class CampaignService : ICampaignService
{
    private const long Tenant = 1;

    private readonly EcommerceDbContext _db;
    private readonly Notifications.IEmailSender _email;

    public CampaignService(EcommerceDbContext db, Notifications.IEmailSender email)
    {
        _db = db;
        _email = email;
    }

    public async Task<List<CampaignDto>> ListAsync(CancellationToken ct = default) =>
        await _db.Campaigns.AsNoTracking()
            .Where(c => c.TenantId == Tenant)
            .OrderByDescending(c => c.CampaignId)
            .Select(c => Map(c))
            .ToListAsync(ct);

    public async Task<CampaignDto> CreateAsync(SaveCampaignRequest req, CancellationToken ct = default)
    {
        Validate(req);
        var c = new Campaign
        {
            TenantId = Tenant,
            Name = req.Name.Trim(),
            Subject = req.Subject.Trim(),
            Body = req.Body,
            Audience = NormalizeAudience(req.Audience),
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Campaigns.Add(c);
        await _db.SaveChangesAsync(ct);
        return Map(c);
    }

    public async Task<CampaignDto?> UpdateAsync(long id, SaveCampaignRequest req, CancellationToken ct = default)
    {
        Validate(req);
        var c = await _db.Campaigns.FirstOrDefaultAsync(x => x.CampaignId == id && x.TenantId == Tenant, ct);
        if (c is null) return null;

        // Editing a campaign mid-send would change the message some people already got.
        if (c.Status is not "Draft")
            throw new AppException("This campaign has already been sent and can no longer be edited.");

        c.Name = req.Name.Trim();
        c.Subject = req.Subject.Trim();
        c.Body = req.Body;
        c.Audience = NormalizeAudience(req.Audience);
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(c);
    }

    public async Task<AudienceCountDto> AudienceCountsAsync(CancellationToken ct = default)
    {
        // Counted in memory rather than with a SQL union. EF cannot shape a Union across two
        // different entity sets, and these are audience lists for one shop — small enough
        // that fetching the addresses costs nothing and reads far more plainly.
        var contacts = await ContactAudience().Select(x => x.Email).ToListAsync(ct);
        var customers = await CustomerAudience().Select(x => x.Email).ToListAsync(ct);

        var c = Unique(contacts);
        var u = Unique(customers);
        return new AudienceCountDto(c.Count, u.Count, Unique(contacts.Concat(customers)).Count);
    }

    /// <summary>
    /// Distinct addresses, case-insensitively — the same person capitalised two ways is one
    /// person, and counting them twice would overstate the audience before a send.
    /// </summary>
    private static HashSet<string> Unique(IEnumerable<string> emails) =>
        emails.Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .ToHashSet();

    /// <summary>
    /// A named type rather than a ValueTuple: EF cannot translate tuple projections, and
    /// composing further onto one throws at query time rather than at compile time.
    /// </summary>
    private sealed record AudienceMember(string Email, string? Name);

    /// <summary>
    /// Opted-in enquirers. Consent is explicit — a contact who never ticked the box is not
    /// in here, however much a bigger list might be wanted.
    /// </summary>
    private IQueryable<AudienceMember> ContactAudience() =>
        _db.Contacts
            .Where(c => c.TenantId == Tenant && c.SubscribedToEmails
                        && c.Email != null && c.Email != "" && c.Status != "Spam")
            .Select(c => new AudienceMember(c.Email!, c.Name));

    /// <summary>
    /// Customers who have actually bought. A purchase is a relationship, not a marketing
    /// permission, so this stays a separate audience the sender has to choose deliberately.
    /// </summary>
    private IQueryable<AudienceMember> CustomerAudience() =>
        _db.Users
            .Where(u => u.TenantId == Tenant && !u.IsDeleted && u.Email != null && u.Email != ""
                        && _db.Orders.Any(o => o.UserId == u.UserId))
            .Select(u => new AudienceMember(u.Email!, u.FullName));

    public async Task<SendResultDto> SendBatchAsync(long id, int batchSize, CancellationToken ct = default)
    {
        var size = Math.Clamp(batchSize, 1, 100);
        var c = await _db.Campaigns.FirstOrDefaultAsync(x => x.CampaignId == id && x.TenantId == Tenant, ct)
            ?? throw new AppException("Campaign not found.", StatusCodes.Status404NotFound);

        if (c.Status == "Sent") throw new AppException("This campaign has already been sent.");

        // First batch resolves the audience into rows. After that the list is fixed, so a
        // contact who unsubscribes mid-send is not chased, and one who subscribes is not
        // silently added to a campaign that was already counted and reported.
        if (c.Status == "Draft")
        {
            var audience = c.Audience switch
            {
                "Customers" => await CustomerAudience().ToListAsync(ct),
                "Both" => (await ContactAudience().ToListAsync(ct))
                    .Concat(await CustomerAudience().ToListAsync(ct)).ToList(),
                _ => await ContactAudience().ToListAsync(ct),
            };

            var unique = audience
                .Where(a => !string.IsNullOrWhiteSpace(a.Email))
                // Case-insensitive: the same person capitalised differently is one person,
                // and the unique index would reject the second row anyway.
                .GroupBy(a => a.Email.Trim().ToLowerInvariant())
                .Select(g => g.First())
                .ToList();

            if (unique.Count == 0) throw new AppException("Nobody is in this audience yet.");

            foreach (var a in unique)
            {
                _db.CampaignRecipients.Add(new CampaignRecipient
                {
                    CampaignId = c.CampaignId,
                    Email = a.Email.Trim(),
                    Name = a.Name,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                });
            }

            c.TotalRecipients = unique.Count;
            c.Status = "Sending";
            c.StartedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        var batch = await _db.CampaignRecipients
            .Where(r => r.CampaignId == c.CampaignId && r.Status == "Pending")
            .OrderBy(r => r.CampaignRecipientId)
            .Take(size)
            .ToListAsync(ct);

        var sent = 0;
        var failed = 0;
        foreach (var r in batch)
        {
            try
            {
                await _email.SendAsync(r.Email, c.Subject, PersonalDot(c.Body, r.Name), ct);
                r.Status = "Sent";
                r.SentAt = DateTime.UtcNow;
                sent++;
            }
            catch (Exception ex)
            {
                // One bad address must not stop the send. Recorded against the row so it can
                // be seen and retried rather than disappearing into a log.
                r.Status = "Failed";
                r.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                failed++;
            }
        }

        c.SentCount += sent;
        c.FailedCount += failed;

        var remaining = await _db.CampaignRecipients
            .CountAsync(r => r.CampaignId == c.CampaignId && r.Status == "Pending", ct) - batch.Count;
        remaining = Math.Max(0, remaining);

        if (remaining == 0)
        {
            c.Status = c.FailedCount > 0 && c.SentCount == 0 ? "Failed" : "Sent";
            c.CompletedAt = DateTime.UtcNow;
        }
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new SendResultDto(batch.Count, sent, failed, remaining, c.Status);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var c = await _db.Campaigns.FirstOrDefaultAsync(x => x.CampaignId == id && x.TenantId == Tenant, ct);
        if (c is null) return false;
        if (c.Status == "Sending") throw new AppException("This campaign is part-way through sending.");
        _db.Campaigns.Remove(c);   // recipients cascade
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Substitutes the one token a campaign supports. Blank names fall back kindly.</summary>
    private static string PersonalDot(string body, string? name) =>
        body.Replace("{{Name}}", string.IsNullOrWhiteSpace(name) ? "there" : name,
            StringComparison.OrdinalIgnoreCase);

    private static void Validate(SaveCampaignRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Give the campaign a name.");
        if (string.IsNullOrWhiteSpace(req.Subject)) throw new AppException("Enter a subject line.");
        if (string.IsNullOrWhiteSpace(req.Body)) throw new AppException("Write the message.");
    }

    private static string NormalizeAudience(string? a) =>
        a is "Customers" or "Both" ? a : "Contacts";

    private static CampaignDto Map(Campaign c) => new(
        c.CampaignId, c.Name, c.Subject, c.Body, c.Audience, c.Status,
        c.TotalRecipients, c.SentCount, c.FailedCount, c.StartedAt, c.CompletedAt, c.CreatedAt);
}
