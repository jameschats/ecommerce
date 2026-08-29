using ecomm.api.Data.Entities;
using ecomm.api.Features.Commerce;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>Native traffic analytics — aggregates page-view events into sessions/visitors, source,
/// device, geo and new-vs-returning.</summary>
public class StorefrontAnalyticsTests
{
    private static CustomerEvent Page(string sid, string path, string? referrer, string device, System.DateTime at) =>
        new() { TenantId = 1, SessionId = sid, EventType = "page", Path = path, Referrer = referrer, Device = device, Country = "IN", Region = "Tamil Nadu", City = "Chennai", CreatedAt = at };

    [Fact]
    public async Task Traffic_aggregates_sessions_source_device_and_new_vs_returning()
    {
        var tenant = new FixedTenant(1);
        var db = TestDb.ForDatabase(System.Guid.NewGuid().ToString(), tenant);
        var now = System.DateTime.UtcNow;

        db.CustomerEvents.AddRange(
            Page("a", "/", null, "desktop", now),
            Page("a", "/products", null, "desktop", now),
            Page("a", "/", null, "desktop", now),
            Page("b", "/", "google.com", "mobile", now),
            Page("b", "/contact", "google.com", "mobile", now),
            // visitor "a" also seen before the 30-day window => returning
            Page("a", "/", null, "desktop", now.AddDays(-40)));
        await db.SaveChangesAsync();

        var svc = new StorefrontAnalyticsService(db);
        var d = await svc.GetAsync(30);
        var t = d.Traffic;

        Assert.Equal(5, t.Pageviews);                 // in-window page views
        Assert.Equal(2, t.Visitors);                  // a, b
        Assert.Equal(1, t.ReturningVisitors);         // a
        Assert.Equal(1, t.NewVisitors);               // b
        Assert.Equal(3, t.ByDevice.First(x => x.Name == "desktop").Count);
        Assert.Equal(2, t.ByDevice.First(x => x.Name == "mobile").Count);
        Assert.Equal(3, t.BySource.First(x => x.Name == "Direct").Count);       // a's 3 direct views
        Assert.Equal(2, t.BySource.First(x => x.Name == "google.com").Count);
        Assert.Equal(3, t.TopPages.First(x => x.Name == "/").Count);            // a×2 + b×1
        Assert.Equal(5, t.ByCity.First(x => x.Name == "Chennai").Count);
    }
}
