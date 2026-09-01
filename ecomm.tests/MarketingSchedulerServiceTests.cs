using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.MarketingStudio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ecomm.tests;

public class MarketingSchedulerServiceTests
{
    private sealed class FakePublisher(bool ok = true) : ISocialPublisher
    {
        public int Calls;
        public Task<PublishResult> PublishAsync(SocialConnection c, MarketingCreative cr, string? caption, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(ok ? PublishResult.Ok("ext-abc") : PublishResult.Fail("boom"));
        }
    }

    private static MarketingSchedulerService New(EcommerceDbContext db, ISocialPublisher? pub = null) =>
        new(db, pub ?? new FakePublisher(), scopeFactory: null!, NullLogger<MarketingSchedulerService>.Instance);

    private static async Task<ScheduledPost> SeedPostAsync(EcommerceDbContext db, string platform, string status, DateTime? at = null)
    {
        var now = DateTime.UtcNow;
        var item = new MarketingPlanItem { MarketingPlanId = 1, Type = "text", Topic = "Spotlight", Channels = platform, ScheduledAt = now, Status = "approved", CreatedAt = now };
        db.MarketingPlanItems.Add(item);
        await db.SaveChangesAsync();
        var creative = new MarketingCreative { MarketingPlanItemId = item.MarketingPlanItemId, Type = "text", Status = "generated", Body = "Hello world caption", CreatedAt = now };
        db.MarketingCreatives.Add(creative);
        await db.SaveChangesAsync();
        var post = new ScheduledPost
        {
            MarketingPlanItemId = item.MarketingPlanItemId, MarketingCreativeId = creative.MarketingCreativeId, Platform = platform,
            ScheduledAt = at ?? now.AddMinutes(-5), Status = status, CreatedAt = now,
        };
        db.ScheduledPosts.Add(post);
        await db.SaveChangesAsync();
        return post;
    }

    private static void Connect(EcommerceDbContext db, string platform)
    {
        db.SocialConnections.Add(new SocialConnection { Platform = platform, ConnectedAt = DateTime.UtcNow, AccessTokenCipher = "x" });
        db.SaveChanges();
    }

    [Fact]
    public async Task Approve_moves_pending_to_scheduled()
    {
        using var db = TestDb.New(tenantId: 1);
        var post = await SeedPostAsync(db, "linkedin", "pending_approval");
        var dto = await New(db).ApproveAsync(post.ScheduledPostId);
        Assert.Equal("scheduled", dto.Status);
    }

    [Fact]
    public async Task Approve_all_pending_returns_count_and_schedules_them()
    {
        using var db = TestDb.New(tenantId: 1);
        await SeedPostAsync(db, "linkedin", "pending_approval");
        await SeedPostAsync(db, "instagram", "pending_approval");
        var n = await New(db).ApproveAllPendingAsync();
        Assert.Equal(2, n);
        Assert.All(db.ScheduledPosts, p => Assert.Equal("scheduled", p.Status));
    }

    [Fact]
    public async Task Reschedule_changes_time_but_not_after_publish()
    {
        using var db = TestDb.New(tenantId: 1);
        var post = await SeedPostAsync(db, "linkedin", "scheduled");
        var newTime = DateTime.UtcNow.AddDays(1);
        var dto = await New(db).RescheduleAsync(post.ScheduledPostId, newTime);
        Assert.Equal(newTime, dto.ScheduledAt);

        post.Status = "published"; await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<AppException>(() => New(db).RescheduleAsync(post.ScheduledPostId, newTime));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Skip_and_delete_work()
    {
        using var db = TestDb.New(tenantId: 1);
        var post = await SeedPostAsync(db, "linkedin", "scheduled");
        Assert.Equal("skipped", (await New(db).SkipAsync(post.ScheduledPostId)).Status);
        await New(db).DeleteAsync(post.ScheduledPostId);
        Assert.Empty(db.ScheduledPosts);
    }

    [Fact]
    public async Task Publish_sweep_publishes_a_due_post_when_the_channel_is_connected()
    {
        using var db = TestDb.New(tenantId: 1);
        var post = await SeedPostAsync(db, "linkedin", "scheduled");
        Connect(db, "linkedin");
        var pub = new FakePublisher(ok: true);

        var n = await New(db, pub).PublishDueForCurrentTenantAsync();

        Assert.Equal(1, n);
        Assert.Equal(1, pub.Calls);
        var reloaded = await db.ScheduledPosts.FirstAsync(p => p.ScheduledPostId == post.ScheduledPostId);
        Assert.Equal("published", reloaded.Status);
        Assert.Equal("ext-abc", reloaded.ExternalPostId);
        Assert.NotNull(reloaded.PublishedAt);
    }

    [Fact]
    public async Task Publish_sweep_fails_clearly_when_the_channel_is_not_connected()
    {
        using var db = TestDb.New(tenantId: 1);
        var post = await SeedPostAsync(db, "linkedin", "scheduled");
        var pub = new FakePublisher(ok: true);

        var n = await New(db, pub).PublishDueForCurrentTenantAsync();

        Assert.Equal(0, n);
        Assert.Equal(0, pub.Calls);                       // never even attempted
        var reloaded = await db.ScheduledPosts.FirstAsync(p => p.ScheduledPostId == post.ScheduledPostId);
        Assert.Equal("failed", reloaded.Status);
        Assert.Contains("Connect", reloaded.Error);
    }

    [Fact]
    public async Task Publish_sweep_ignores_posts_not_yet_due()
    {
        using var db = TestDb.New(tenantId: 1);
        await SeedPostAsync(db, "linkedin", "scheduled", at: DateTime.UtcNow.AddHours(3));   // future
        Connect(db, "linkedin");

        var n = await New(db).PublishDueForCurrentTenantAsync();
        Assert.Equal(0, n);
    }

    [Fact]
    public async Task List_joins_topic_and_preview()
    {
        using var db = TestDb.New(tenantId: 1);
        await SeedPostAsync(db, "linkedin", "pending_approval");
        var list = await New(db).ListAsync(null);
        var row = Assert.Single(list);
        Assert.Equal("Spotlight", row.Topic);
        Assert.Equal("Hello world caption", row.Preview);
    }
}
