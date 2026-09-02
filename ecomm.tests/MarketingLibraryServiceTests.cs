using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.MarketingStudio;
using Xunit;

namespace ecomm.tests;

public class MarketingLibraryServiceTests
{
    private static async Task<(long itemId, long creativeId)> SeedCreativeAsync(
        EcommerceDbContext db, string type, string? mediaUrl = null)
    {
        var now = DateTime.UtcNow;
        var item = new MarketingPlanItem { MarketingPlanId = 1, Type = type, Topic = "T", Channels = "", Status = "approved", ScheduledAt = now, CreatedAt = now };
        db.MarketingPlanItems.Add(item);
        await db.SaveChangesAsync();
        var creative = new MarketingCreative { MarketingPlanItemId = item.MarketingPlanItemId, Type = type, Status = "generated", Body = "Caption", OutputMediaUrl = mediaUrl, CreatedAt = now };
        db.MarketingCreatives.Add(creative);
        await db.SaveChangesAsync();
        return (item.MarketingPlanItemId, creative.MarketingCreativeId);
    }

    [Fact]
    public async Task Lists_creatives_newest_first()
    {
        using var db = TestDb.New(tenantId: 1);
        await SeedCreativeAsync(db, "text");
        await SeedCreativeAsync(db, "poster", "https://cdn/p.svg");

        var list = await new MarketingLibraryService(db).ListAsync(null);

        Assert.Equal(2, list.Count);
        Assert.Equal("poster", list[0].Type);              // most recently created first
        Assert.Equal("https://cdn/p.svg", list[0].MediaUrl);
    }

    [Fact]
    public async Task Filters_by_type()
    {
        using var db = TestDb.New(tenantId: 1);
        await SeedCreativeAsync(db, "text");
        await SeedCreativeAsync(db, "poster");

        var posters = await new MarketingLibraryService(db).ListAsync("poster");

        Assert.Single(posters);
        Assert.Equal("poster", posters[0].Type);
    }

    [Fact]
    public async Task Reports_the_channels_a_creative_is_already_scheduled_to()
    {
        using var db = TestDb.New(tenantId: 1);
        var (itemId, creativeId) = await SeedCreativeAsync(db, "poster");
        db.ScheduledPosts.Add(new ScheduledPost { MarketingPlanItemId = itemId, MarketingCreativeId = creativeId, Platform = "linkedin", ScheduledAt = DateTime.UtcNow, Status = "pending_approval", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var list = await new MarketingLibraryService(db).ListAsync(null);

        Assert.Equal(new[] { "linkedin" }, list[0].Channels);
    }

    [Fact]
    public async Task Unscheduled_creatives_report_an_empty_channel_list()
    {
        using var db = TestDb.New(tenantId: 1);
        await SeedCreativeAsync(db, "poster");
        var list = await new MarketingLibraryService(db).ListAsync(null);
        Assert.Empty(list[0].Channels);
    }
}
