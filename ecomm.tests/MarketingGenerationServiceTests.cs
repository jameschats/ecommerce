using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.MarketingStudio;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class MarketingGenerationServiceTests
{
    private sealed class FakeCopywriter : IMarketingCopywriter
    {
        public int Calls;
        public Task<string> WriteAsync(string kind, long? productId, string brief, long? userId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult($"[{kind}] {brief}");
        }
    }

    private static async Task<long> SeedPlanAsync(EcommerceDbContext db, params MarketingPlanItem[] items)
    {
        var plan = new MarketingPlan { WeekStart = DateTime.UtcNow.Date, Status = "confirmed", CreatedAt = DateTime.UtcNow };
        db.MarketingPlans.Add(plan);
        await db.SaveChangesAsync();
        foreach (var it in items) { it.MarketingPlanId = plan.MarketingPlanId; it.CreatedAt = DateTime.UtcNow; db.MarketingPlanItems.Add(it); }
        await db.SaveChangesAsync();
        return plan.MarketingPlanId;
    }

    private static MarketingPlanItem TextItem(string channels, string topic = "Topic") =>
        new() { Type = "text", Topic = topic, Channels = channels, Status = "proposed", ScheduledAt = DateTime.UtcNow.Date.AddHours(10) };

    [Fact]
    public async Task Generates_one_creative_per_item_and_fans_out_a_post_per_channel()
    {
        using var db = TestDb.New(tenantId: 1);
        var planId = await SeedPlanAsync(db, TextItem("linkedin,instagram"), TextItem("whatsapp"));
        var copy = new FakeCopywriter();

        var result = await new MarketingGenerationService(db, copy).GenerateForPlanAsync(planId, userId: 7);

        Assert.Equal(2, result.CreativesGenerated);
        Assert.Equal(3, result.PostsScheduled);                 // 2 + 1 channels
        Assert.Equal(2, copy.Calls);                            // one caption per item (reused across channels)
        Assert.Equal(2, db.MarketingCreatives.Count());
        Assert.Equal(3, db.ScheduledPosts.Count());
        Assert.All(db.ScheduledPosts, p => Assert.Equal("pending_approval", p.Status));
    }

    [Fact]
    public async Task Marks_generated_items_approved_and_is_idempotent()
    {
        using var db = TestDb.New(tenantId: 1);
        var planId = await SeedPlanAsync(db, TextItem("linkedin"));
        var svc = new MarketingGenerationService(db, new FakeCopywriter());

        await svc.GenerateForPlanAsync(planId, null);
        var second = await svc.GenerateForPlanAsync(planId, null);   // nothing left to generate

        Assert.Equal(0, second.CreativesGenerated);
        Assert.Equal("approved", (await db.MarketingPlanItems.FirstAsync()).Status);
        Assert.Single(db.MarketingCreatives);
    }

    [Fact]
    public async Task Skips_items_with_no_channel_without_spending_a_call()
    {
        using var db = TestDb.New(tenantId: 1);
        var planId = await SeedPlanAsync(db, TextItem(""));
        var copy = new FakeCopywriter();

        var result = await new MarketingGenerationService(db, copy).GenerateForPlanAsync(planId, null);

        Assert.Equal(0, result.CreativesGenerated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, copy.Calls);
    }

    [Fact]
    public async Task Skips_poster_items_in_this_slice()
    {
        using var db = TestDb.New(tenantId: 1);
        var poster = new MarketingPlanItem { Type = "poster", Topic = "P", Channels = "instagram", Status = "proposed", ScheduledAt = DateTime.UtcNow };
        var planId = await SeedPlanAsync(db, poster);

        var result = await new MarketingGenerationService(db, new FakeCopywriter()).GenerateForPlanAsync(planId, null);

        Assert.Equal(0, result.CreativesGenerated);
        Assert.Equal(1, result.Skipped);
    }
}
