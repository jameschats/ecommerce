using ecomm.api.Data.Entities;
using ecomm.api.Features.SuperAdmin;
using Xunit;

namespace ecomm.tests;

public class PlatformAnnouncementTests
{
    [Fact]
    public async Task Active_excludes_hidden_expired_and_future()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new PlatformAnnouncementService(db);
        var now = DateTime.UtcNow;
        db.PlatformAnnouncements.Add(new PlatformAnnouncement { Title = "Shown", Body = "b", Level = "info", IsActive = true, CreatedAt = now });
        db.PlatformAnnouncements.Add(new PlatformAnnouncement { Title = "Hidden", Body = "b", Level = "info", IsActive = false, CreatedAt = now });
        db.PlatformAnnouncements.Add(new PlatformAnnouncement { Title = "Expired", Body = "b", Level = "info", IsActive = true, EndsAt = now.AddDays(-1), CreatedAt = now });
        db.PlatformAnnouncements.Add(new PlatformAnnouncement { Title = "Future", Body = "b", Level = "info", IsActive = true, StartsAt = now.AddDays(1), CreatedAt = now });
        await db.SaveChangesAsync();

        var active = await svc.ActiveAsync(now, default);

        Assert.Single(active);
        Assert.Equal("Shown", active[0].Title);
    }

    [Fact]
    public async Task Create_normalizes_bad_level_to_info()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new PlatformAnnouncementService(db);

        var created = await svc.CreateAsync(new AnnouncementUpsert("Heads up", "Body", "banana", null, null), default);

        Assert.Equal("info", created.Level);
        Assert.True(created.IsActive);
    }
}
