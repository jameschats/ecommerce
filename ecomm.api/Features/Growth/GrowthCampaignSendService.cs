using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Features.Customers;
using ecomm.api.Features.Notifications;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Growth;

public sealed record SendCampaignRequest(string? Segment, DateTime? ScheduledAt);

public sealed record CampaignSendStatusDto(
    long CampaignId, string Status, string Channel, string? Segment,
    DateTime? ScheduledAt, DateTime? SentAt,
    int RecipientCount, int SentCount, int FailedCount,
    int EligibleNow, bool HasEmailContent);

public interface IGrowthCampaignSendService
{
    /// <summary>The consented-recipient count for a segment right now, plus whether the campaign has email copy to send.</summary>
    Task<CampaignSendStatusDto> PreviewAsync(long campaignId, string? segment, CancellationToken ct = default);
    /// <summary>Schedule (or send now, when ScheduledAt is null/past) this campaign's email to a segment.</summary>
    Task<CampaignSendStatusDto> ScheduleAsync(long campaignId, SendCampaignRequest req, CancellationToken ct = default);
    /// <summary>Cancel a scheduled-but-not-yet-sent send. The queued job no-ops once the status is no longer Scheduled.</summary>
    Task<CampaignSendStatusDto> CancelAsync(long campaignId, CancellationToken ct = default);
    /// <summary>Hangfire entry point. Re-establishes the tenant scope and sends to every consented recipient.</summary>
    Task SendAsync(long campaignId, long tenantId, CancellationToken ct = default);
}

/// <summary>
/// AI Growth M1 — turns a generated campaign into an actual send. The campaign's <c>email</c>-channel
/// <see cref="Data.Entities.GrowthContent"/> row (subject = Title, body = Body) is emailed to every
/// customer in the chosen segment who has opted in to email marketing. Sends run on Hangfire so a
/// scheduled campaign fires later and a large audience never blocks the request thread.
///
/// The send job runs outside a request, so it re-establishes the tenant scope via
/// <see cref="ICurrentTenantService.BeginScope"/> — without it the tenant-scoped customer/content
/// queries would silently target the default tenant and cross-send one store's campaign to another's list.
/// </summary>
public sealed class GrowthCampaignSendService(
    EcommerceDbContext db, IEmailSender email, ICustomerAdminService customers,
    ICurrentTenantService tenant, IBackgroundJobClient jobs, ILogger<GrowthCampaignSendService> log)
    : IGrowthCampaignSendService
{
    private const int MaxRecipientsPerSend = 5000;   // safety cap; SMB lists are far smaller
    private static readonly HashSet<string> KnownSegments =
        new(new[] { "all", "paid", "repeat", "prospect", "subscribers" }, StringComparer.OrdinalIgnoreCase);

    public async Task<CampaignSendStatusDto> PreviewAsync(long campaignId, string? segment, CancellationToken ct = default)
    {
        var c = await Load(campaignId, ct);
        var hasEmail = await HasEmailContentAsync(campaignId, ct);
        var eligible = (await customers.EmailRecipientsAsync(Normalize(segment ?? c.SegmentKey), ct)).Count;
        return ToStatus(c, eligible, hasEmail);
    }

    public async Task<CampaignSendStatusDto> ScheduleAsync(long campaignId, SendCampaignRequest req, CancellationToken ct = default)
    {
        var c = await Load(campaignId, ct);
        if (c.Status is "Sending")
            throw new AppException("This campaign is already being sent.", StatusCodes.Status409Conflict);

        var segment = Normalize(req.Segment);
        if (segment is not null && !KnownSegments.Contains(segment))
            throw new AppException("Unknown customer segment.", StatusCodes.Status400BadRequest);

        if (!await HasEmailContentAsync(campaignId, ct))
            throw new AppException("This campaign has no email content to send. Generate the email channel first.", StatusCodes.Status400BadRequest);

        var eligible = (await customers.EmailRecipientsAsync(segment, ct)).Count;
        if (eligible == 0)
            throw new AppException("No customers in this segment have opted in to email marketing.", StatusCodes.Status400BadRequest);

        var now = DateTime.UtcNow;
        var when = req.ScheduledAt is { } t && t > now ? t.ToUniversalTime() : now;
        var immediate = when <= now;

        c.Channel = "email";
        c.SegmentKey = segment;
        c.ScheduledAt = when;
        c.SentAt = null;
        c.RecipientCount = eligible;
        c.SentCount = 0;
        c.FailedCount = 0;
        c.Status = "Scheduled";
        c.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        var tenantId = tenant.CurrentTenantId;   // capture now; the job runs with no ambient tenant
        if (immediate)
            jobs.Enqueue<IGrowthCampaignSendService>(s => s.SendAsync(campaignId, tenantId, CancellationToken.None));
        else
            jobs.Schedule<IGrowthCampaignSendService>(s => s.SendAsync(campaignId, tenantId, CancellationToken.None), new DateTimeOffset(when, TimeSpan.Zero));

        return ToStatus(c, eligible, true);
    }

    public async Task<CampaignSendStatusDto> CancelAsync(long campaignId, CancellationToken ct = default)
    {
        var c = await Load(campaignId, ct);
        if (c.Status != "Scheduled")
            throw new AppException("Only a scheduled send can be cancelled.", StatusCodes.Status400BadRequest);
        c.Status = "Draft";
        c.ScheduledAt = null;
        c.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToStatus(c, await SafeEligible(c.SegmentKey, ct), await HasEmailContentAsync(campaignId, ct));
    }

    public async Task SendAsync(long campaignId, long tenantId, CancellationToken ct = default)
    {
        using (tenant.BeginScope(tenantId))
        {
            var c = await db.GrowthCampaigns.FirstOrDefaultAsync(x => x.GrowthCampaignId == campaignId, ct);
            if (c is null) { log.LogWarning("Campaign {Id} vanished before send.", campaignId); return; }
            if (c.Status is not ("Scheduled" or "Sending")) { log.LogInformation("Campaign {Id} no longer sendable ({Status}); skipping.", campaignId, c.Status); return; }

            c.Status = "Sending";
            await db.SaveChangesAsync(ct);

            var content = await db.GrowthContents.AsNoTracking()
                .Where(g => g.CampaignId == campaignId && g.ContentType == "email")
                .OrderByDescending(g => g.GrowthContentId)
                .Select(g => new { g.Title, g.Body })
                .FirstOrDefaultAsync(ct);
            if (content is null || string.IsNullOrWhiteSpace(content.Body))
            {
                c.Status = "Failed"; c.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
                log.LogWarning("Campaign {Id} has no email content at send time.", campaignId);
                return;
            }

            var storeName = await db.Tenants.AsNoTracking().Where(t => t.TenantId == tenantId).Select(t => t.Name).FirstOrDefaultAsync(ct);
            var subject = string.IsNullOrWhiteSpace(content.Title) ? c.Name : content.Title!.Trim();
            var html = ToHtml(content.Body);

            var recipients = await customers.EmailRecipientsAsync(c.SegmentKey, ct);
            if (recipients.Count > MaxRecipientsPerSend)
            {
                log.LogWarning("Campaign {Id} segment has {Count} recipients; capping at {Cap}.", campaignId, recipients.Count, MaxRecipientsPerSend);
                recipients = recipients.Take(MaxRecipientsPerSend).ToList();
            }

            int sent = 0, failed = 0;
            foreach (var r in recipients)
            {
                ct.ThrowIfCancellationRequested();
                try { await email.SendAsync(r.Email, subject, html, ct, fromName: storeName); sent++; }
                catch (Exception ex) { failed++; log.LogWarning(ex, "Campaign {Id} email to {Email} failed.", campaignId, r.Email); }
            }

            c.RecipientCount = recipients.Count;
            c.SentCount = sent;
            c.FailedCount = failed;
            c.SentAt = DateTime.UtcNow;
            c.Status = sent == 0 && failed > 0 ? "Failed" : "Sent";
            c.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            log.LogInformation("Campaign {Id} sent: {Sent} ok, {Failed} failed of {Total}.", campaignId, sent, failed, recipients.Count);
        }
    }

    // ---- helpers ----

    private async Task<Data.Entities.GrowthCampaign> Load(long id, CancellationToken ct) =>
        await db.GrowthCampaigns.FirstOrDefaultAsync(c => c.GrowthCampaignId == id, ct)
        ?? throw new AppException("Campaign not found.", StatusCodes.Status404NotFound);

    private Task<bool> HasEmailContentAsync(long campaignId, CancellationToken ct) =>
        db.GrowthContents.AnyAsync(g => g.CampaignId == campaignId && g.ContentType == "email" && g.Body != "", ct);

    private async Task<int> SafeEligible(string? segment, CancellationToken ct)
    {
        try { return (await customers.EmailRecipientsAsync(segment, ct)).Count; }
        catch { return 0; }
    }

    private static string? Normalize(string? segment) =>
        string.IsNullOrWhiteSpace(segment) || segment.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : segment.Trim().ToLowerInvariant();

    private static CampaignSendStatusDto ToStatus(Data.Entities.GrowthCampaign c, int eligibleNow, bool hasEmail) =>
        new(c.GrowthCampaignId, c.Status, c.Channel, c.SegmentKey, c.ScheduledAt, c.SentAt,
            c.RecipientCount, c.SentCount, c.FailedCount, eligibleNow, hasEmail);

    /// <summary>AI copy is plain text with line breaks; render it as safe HTML paragraphs.</summary>
    private static string ToHtml(string body)
    {
        var paras = body.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var p in paras)
        {
            var encoded = System.Net.WebUtility.HtmlEncode(p.Trim()).Replace("\n", "<br>");
            sb.Append("<p>").Append(encoded).Append("</p>");
        }
        return sb.Length == 0 ? $"<p>{System.Net.WebUtility.HtmlEncode(body)}</p>" : sb.ToString();
    }
}
