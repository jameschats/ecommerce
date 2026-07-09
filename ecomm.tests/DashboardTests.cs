using ecomm.api.Features.Analytics;
using ecomm.api.Features.Dashboard;
using ecomm.api.Features.Settings;
using Xunit;

namespace ecomm.tests;

public class DashboardTests
{
    private static DashboardService NewService(out ecomm.api.Data.Context.EcommerceDbContext db)
    {
        db = TestDb.New(tenantId: 1);
        return new DashboardService(db, new AnalyticsService(db), new StoreSettingsService(db));
    }

    [Fact]
    public async Task Fresh_store_has_full_checklist_none_done()
    {
        var svc = NewService(out var db);
        using var _ = db;

        var d = await svc.GetAsync();

        Assert.Equal(5, d.ChecklistTotal);
        Assert.Equal(0, d.ChecklistDone);
        Assert.All(d.Checklist, i => Assert.False(i.Done));
    }

    [Fact]
    public async Task Choosing_no_gst_completes_the_tax_step()
    {
        var svc = NewService(out var db);
        using var _ = db;

        await new StoreSettingsService(db).UpdateAsync(
            new UpdateStoreSettingsRequest("None", null, null, null, false, null, null, null, null));

        var d = await svc.GetAsync();

        Assert.True(d.Checklist.Single(i => i.Key == "tax").Done);
        Assert.Equal(1, d.ChecklistDone);
    }
}
