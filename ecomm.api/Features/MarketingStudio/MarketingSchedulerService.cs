using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>One row on the scheduler / history screen: a scheduled post plus its creative preview.</summary>
public sealed record ScheduledPostDto(
    long Id, long PlanItemId, string Platform, DateTime ScheduledAt, string Status,
    string Type, string Topic, string? Preview, string? MediaUrl, string? ExternalPostId, string? Error, DateTime? PublishedAt);

public interface IMarketingSchedulerService
{
    Task<IReadOnlyList<ScheduledPostDto>> ListAsync(string? status, CancellationToken ct = default);
    Task<ScheduledPostDto> ApproveAsync(long id, CancellationToken ct = default);
    Task<int> ApproveAllPendingAsync(CancellationToken ct = default);
    Task<ScheduledPostDto> RescheduleAsync(long id, DateTime scheduledAt, CancellationToken ct = default);
    Task<ScheduledPostDto> SkipAsync(long id, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    /// <summary>Cross-tenant Hangfire sweep: publish every due (scheduled, past-due) post.</summary>
    Task<int> RunPublishSweepAsync(CancellationToken ct = default);
    /// <summary>Publish all due posts for the CURRENT tenant scope. Returns how many published.</summary>
    Task<int> PublishDueForCurrentTenantAsync(CancellationToken ct = default);
}

/// <summary>
/// The scheduler + publish sweep (MS2 sub-step 4). The screen lists per-channel scheduled posts and
/// lets the merchant approve (pending_approval→scheduled), reschedule, skip or delete — the D5
/// approval gate. A Hangfire sweep publishes due scheduled posts via <see cref="ISocialPublisher"/>
/// using the tenant's MS1 connection tokens; a platform with no live connection is failed with a clear
/// "connect it first" message rather than silently stuck. ScheduledPost doubles as the job history.
/// </summary>
public sealed class MarketingSchedulerService(
    EcommerceDbContext db, ISocialPublisher publisher,
    IServiceScopeFactory scopeFactory, ILogger<MarketingSchedulerService> log) : IMarketingSchedulerService
{
    public async Task<IReadOnlyList<ScheduledPostDto>> ListAsync(string? status, CancellationToken ct = default)
    {
        var q = db.ScheduledPosts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(p => p.Status == status);

        var posts = await q.OrderBy(p => p.ScheduledAt).Take(500).ToListAsync(ct);
        if (posts.Count == 0) return [];

        var itemIds = posts.Select(p => p.MarketingPlanItemId).Distinct().ToList();
        var creativeIds = posts.Select(p => p.MarketingCreativeId).Distinct().ToList();
        var topics = await db.MarketingPlanItems.AsNoTracking().Where(i => itemIds.Contains(i.MarketingPlanItemId))
            .ToDictionaryAsync(i => i.MarketingPlanItemId, i => new { i.Topic, i.Type }, ct);
        var creatives = await db.MarketingCreatives.AsNoTracking().Where(c => creativeIds.Contains(c.MarketingCreativeId))
            .ToDictionaryAsync(c => c.MarketingCreativeId, c => new { c.Body, c.OutputMediaUrl }, ct);

        return posts.Select(p =>
        {
            topics.TryGetValue(p.MarketingPlanItemId, out var t);
            creatives.TryGetValue(p.MarketingCreativeId, out var cr);
            return new ScheduledPostDto(p.ScheduledPostId, p.MarketingPlanItemId, p.Platform, p.ScheduledAt, p.Status,
                t?.Type ?? "text", t?.Topic ?? "", Preview(cr?.Body), cr?.OutputMediaUrl, p.ExternalPostId, p.Error, p.PublishedAt);
        }).ToList();
    }

    public async Task<ScheduledPostDto> ApproveAsync(long id, CancellationToken ct = default)
    {
        var p = await Require(id, ct);
        if (p.Status is "pending_approval" or "failed" or "skipped") { p.Status = "scheduled"; p.Error = null; p.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
        return await MapOne(p, ct);
    }

    public async Task<int> ApproveAllPendingAsync(CancellationToken ct = default)
    {
        var pending = await db.ScheduledPosts.Where(p => p.Status == "pending_approval").ToListAsync(ct);
        foreach (var p in pending) { p.Status = "scheduled"; p.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct);
        return pending.Count;
    }

    public async Task<ScheduledPostDto> RescheduleAsync(long id, DateTime scheduledAt, CancellationToken ct = default)
    {
        var p = await Require(id, ct);
        if (p.Status is "published") throw new AppException("This post has already been published.", StatusCodes.Status409Conflict);
        p.ScheduledAt = scheduledAt;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await MapOne(p, ct);
    }

    public async Task<ScheduledPostDto> SkipAsync(long id, CancellationToken ct = default)
    {
        var p = await Require(id, ct);
        if (p.Status is not "published") { p.Status = "skipped"; p.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
        return await MapOne(p, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var p = await db.ScheduledPosts.FirstOrDefaultAsync(x => x.ScheduledPostId == id, ct);
        if (p is null) return;
        db.ScheduledPosts.Remove(p);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> RunPublishSweepAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // Distinct tenants with a due post (bypass the tenant filter to enumerate, then scope per tenant).
        var tenants = await db.ScheduledPosts.IgnoreQueryFilters()
            .Where(p => p.Status == "scheduled" && p.ScheduledAt <= now)
            .Select(p => p.TenantId).Distinct().ToListAsync(ct);

        var published = 0;
        foreach (var tenantId in tenants)
        {
            using var scope = scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMarketingSchedulerService>();
            var scopedTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
            using (scopedTenant.BeginScope(tenantId))
            {
                try { published += await svc.PublishDueForCurrentTenantAsync(ct); }
                catch (Exception ex) { log.LogWarning(ex, "Publish sweep failed for tenant {Tenant}.", tenantId); }
            }
        }
        return published;
    }

    public async Task<int> PublishDueForCurrentTenantAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var duePosts = await db.ScheduledPosts
            .Where(p => p.Status == "scheduled" && p.ScheduledAt <= now)
            .OrderBy(p => p.ScheduledAt).Take(200).ToListAsync(ct);

        var published = 0;
        foreach (var p in duePosts)
        {
            try { if (await PublishOneAsync(p.ScheduledPostId, ct)) published++; }
            catch (Exception ex) { log.LogWarning(ex, "Publish failed for post {Post}.", p.ScheduledPostId); }
        }
        return published;
    }

    /// <summary>Publish a single due post under the current tenant scope. Returns true on success.</summary>
    private async Task<bool> PublishOneAsync(long postId, CancellationToken ct)
    {
        var p = await db.ScheduledPosts.FirstOrDefaultAsync(x => x.ScheduledPostId == postId && x.Status == "scheduled", ct);
        if (p is null) return false;
        var now = DateTime.UtcNow;

        var connection = await db.SocialConnections.FirstOrDefaultAsync(c => c.Platform == p.Platform, ct);
        var connected = connection is not null && (connection.ExpiresAt is null || connection.ExpiresAt > now);
        if (!connected)
        {
            p.Status = "failed";
            p.Error = $"Connect your {p.Platform} account to publish this post.";
            p.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return false;
        }

        var creative = await db.MarketingCreatives.FirstOrDefaultAsync(c => c.MarketingCreativeId == p.MarketingCreativeId, ct);
        if (creative is null) { p.Status = "failed"; p.Error = "The creative for this post is missing."; p.UpdatedAt = now; await db.SaveChangesAsync(ct); return false; }

        var result = await publisher.PublishAsync(connection!, creative, creative.Body, ct);
        if (result.Success) { p.Status = "published"; p.ExternalPostId = result.ExternalPostId; p.PublishedAt = now; p.Error = null; }
        else { p.Status = "failed"; p.Error = result.Error; }
        p.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return result.Success;
    }

    // ---- helpers ----
    private async Task<Data.Entities.ScheduledPost> Require(long id, CancellationToken ct) =>
        await db.ScheduledPosts.FirstOrDefaultAsync(x => x.ScheduledPostId == id, ct)
        ?? throw new AppException("Scheduled post not found.", StatusCodes.Status404NotFound);

    private async Task<ScheduledPostDto> MapOne(Data.Entities.ScheduledPost p, CancellationToken ct)
    {
        var item = await db.MarketingPlanItems.AsNoTracking().FirstOrDefaultAsync(i => i.MarketingPlanItemId == p.MarketingPlanItemId, ct);
        var cr = await db.MarketingCreatives.AsNoTracking().Where(c => c.MarketingCreativeId == p.MarketingCreativeId)
            .Select(c => new { c.Body, c.OutputMediaUrl }).FirstOrDefaultAsync(ct);
        return new ScheduledPostDto(p.ScheduledPostId, p.MarketingPlanItemId, p.Platform, p.ScheduledAt, p.Status,
            item?.Type ?? "text", item?.Topic ?? "", Preview(cr?.Body), cr?.OutputMediaUrl, p.ExternalPostId, p.Error, p.PublishedAt);
    }

    private static string? Preview(string? body) =>
        string.IsNullOrWhiteSpace(body) ? null : (body.Length <= 140 ? body : body[..140] + "…");
}
