using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Features.MarketingStudio;
using Xunit;

namespace ecomm.tests;

public class MarketingPlanServiceTests
{
    private sealed class FakeSettings(int text, int posters, params string[] channels) : IMarketingPlanSettingsService
    {
        public Task<MarketingPlanSettingsDto> GetAsync(CancellationToken ct = default)
        {
            var chans = channels.Select(p => new ChannelPrefDto(p, p, true, true, true, true, true)).ToList();
            return Task.FromResult(new MarketingPlanSettingsDto(text, posters, 0, 1, 10, false, chans));
        }
        public Task<MarketingPlanSettingsDto> SaveAsync(MarketingPlanSettingsDto req, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeCatalog(int n) : ICatalogReader
    {
        public Task<IReadOnlyList<CatalogProduct>> TopProductsAsync(int count, CancellationToken ct = default)
        {
            IReadOnlyList<CatalogProduct> list = Enumerable.Range(1, n)
                .Select(i => new CatalogProduct(i, $"Product {i}", 100m * i)).ToList();
            return Task.FromResult(list);
        }
        public Task<string?> ProductNameAsync(long productId, CancellationToken ct = default) => Task.FromResult<string?>($"Product {productId}");
    }

    private static MarketingPlanService New(EcommerceDbContext db, int text = 2, int posters = 2, params string[] channels) =>
        new(db, new FakeSettings(text, posters, channels.Length == 0 ? new[] { "linkedin" } : channels), new FakeCatalog(5));

    [Fact]
    public async Task Propose_creates_a_draft_with_the_configured_counts_and_channels()
    {
        using var db = TestDb.New(tenantId: 1);
        var plan = await New(db, text: 3, posters: 2, "linkedin", "instagram").ProposeAsync(new ProposePlanRequest(null));

        Assert.Equal("draft", plan.Status);
        Assert.Equal(5, plan.Items.Count);                                  // 3 text + 2 posters
        Assert.Equal(2, plan.Items.Count(i => i.Type == "poster"));
        Assert.All(plan.Items, i => Assert.Contains("linkedin", i.Channels));
        Assert.All(plan.Items, i => Assert.True(i.ScheduledAt >= plan.WeekStart));
    }

    [Fact]
    public async Task Propose_replaces_the_previous_draft()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, text: 2, posters: 1);
        await svc.ProposeAsync(new ProposePlanRequest(null));
        await svc.ProposeAsync(new ProposePlanRequest(null));

        Assert.Single(db.MarketingPlans);                                    // only the latest draft survives
    }

    [Fact]
    public async Task Get_current_returns_the_draft_with_items_in_date_order()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, text: 2, posters: 2);
        await svc.ProposeAsync(new ProposePlanRequest(null));

        var cur = await svc.GetCurrentAsync();
        Assert.NotNull(cur);
        Assert.Equal(4, cur!.Items.Count);
        for (var i = 1; i < cur.Items.Count; i++)
            Assert.True(cur.Items[i].ScheduledAt >= cur.Items[i - 1].ScheduledAt);
    }

    [Fact]
    public async Task Update_item_changes_topic_and_channels()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, text: 1, posters: 0, "linkedin", "pinterest");
        var plan = await svc.ProposeAsync(new ProposePlanRequest(null));
        var item = plan.Items[0];

        var updated = await svc.UpdateItemAsync(item.Id,
            new UpdatePlanItemRequest(null, "My custom topic", null, new[] { "pinterest" }, false, null));

        Assert.Equal("My custom topic", updated.Topic);
        Assert.Equal(new[] { "pinterest" }, updated.Channels);
        Assert.False(updated.IncludeLogo);
    }

    [Fact]
    public async Task Add_and_remove_item_adjust_the_plan()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, text: 1, posters: 0);
        await svc.ProposeAsync(new ProposePlanRequest(null));

        var added = await svc.AddItemAsync(new AddPlanItemRequest("poster", 3, "Extra poster", DateTime.UtcNow.Date.AddDays(2).AddHours(9), new[] { "linkedin" }));
        Assert.Equal(2, (await svc.GetCurrentAsync())!.Items.Count);

        await svc.RemoveItemAsync(added.Id);
        Assert.Single((await svc.GetCurrentAsync())!.Items);
    }

    [Fact]
    public async Task Confirm_sets_status_and_blocks_further_edits()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, text: 1, posters: 1);
        var plan = await svc.ProposeAsync(new ProposePlanRequest(null));

        var confirmed = await svc.ConfirmAsync(plan.Id);
        Assert.Equal("confirmed", confirmed.Status);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.UpdateItemAsync(plan.Items[0].Id, new UpdatePlanItemRequest(null, "nope", null, null, null, null)));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Discard_removes_the_plan_and_items()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, text: 2, posters: 2);
        var plan = await svc.ProposeAsync(new ProposePlanRequest(null));

        await svc.DiscardAsync(plan.Id);
        Assert.Empty(db.MarketingPlans);
        Assert.Empty(db.MarketingPlanItems);
    }
}
