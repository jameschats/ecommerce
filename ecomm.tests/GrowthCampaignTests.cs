using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Growth;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// Campaign builder (G2). A campaign is one goal fanned out to four channels; these check the fan-out
/// produces four linked pieces, the goal shapes the brief, and — the part that matters — a mid-fan-out
/// failure keeps what succeeded instead of losing the campaign.
/// </summary>
public class GrowthCampaignTests
{
    private static (EcommerceDbContext db, GrowthCampaignService svc, CountingAi ai) Setup()
    {
        var db = TestDb.New(tenantId: 1);
        db.Categories.Add(new Category { CategoryId = 1, TenantId = 1, Name = "Sarees", Slug = "sarees", CreatedAt = DateTime.UtcNow });
        db.Products.Add(new Product
        {
            ProductId = 9, TenantId = 1, CategoryId = 1, Name = "Silk Saree",
            Slug = "silk", Sku = "S1", Price = 3999m, CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        var ai = new CountingAi();
        var gen = new GrowthGenerationService(db, new PassThroughCredits(ai), new BrandKitService(db));
        return (db, new GrowthCampaignService(db, gen), ai);
    }

    [Fact]
    public async Task A_campaign_fans_out_to_four_linked_channels()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;

        var campaign = await svc.CreateAsync(new CreateCampaignRequest("new-arrival", null, 9, null, null), userId: 1);

        Assert.Equal(4, ai.Calls);                               // one AI call per channel
        Assert.Equal(4, campaign.Channels.Count);
        Assert.All(campaign.Channels, c => Assert.NotNull(c.Content));
        Assert.Equal(new[] { "instagram-caption", "facebook-post", "whatsapp", "email" },
            campaign.Channels.Select(c => c.Channel));

        // Every piece is persisted and linked back to the campaign.
        var pieces = await db.GrowthContents.Where(c => c.CampaignId == campaign.Id).ToListAsync();
        Assert.Equal(4, pieces.Count);
    }

    [Fact]
    public async Task The_goal_shapes_the_prompt_for_each_channel()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;

        await svc.CreateAsync(new CreateCampaignRequest("restock", null, 9, null, null), 1);

        Assert.Contains("back in stock", ai.LastUser, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_merchant_note_is_appended_to_the_goal_brief()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;

        await svc.CreateAsync(new CreateCampaignRequest("festival", null, 9, "Diwali, mention free gift wrap", null), 1);

        Assert.Contains("Diwali, mention free gift wrap", ai.LastUser);
    }

    [Fact]
    public async Task A_default_name_is_built_from_the_goal_and_product()
    {
        var (db, svc, _) = Setup();
        using var _db = db;

        var campaign = await svc.CreateAsync(new CreateCampaignRequest("weekend-sale", null, 9, null, null), 1);

        Assert.Equal("Weekend sale — Silk Saree", campaign.Name);
    }

    [Fact]
    public async Task A_channel_that_fails_is_reported_and_the_rest_are_kept()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;
        ai.FailOnCall = 3;   // the third channel (whatsapp) throws

        var campaign = await svc.CreateAsync(new CreateCampaignRequest("new-arrival", null, 9, null, null), 1);

        Assert.Equal(4, campaign.Channels.Count);
        Assert.Equal(3, campaign.Channels.Count(c => c.Content is not null));
        var failed = Assert.Single(campaign.Channels, c => c.Content is null);
        Assert.Equal("whatsapp", failed.Channel);
        Assert.NotNull(failed.Error);

        // The campaign and the three good pieces survive.
        Assert.Equal(3, await db.GrowthContents.CountAsync(c => c.CampaignId == campaign.Id));
        Assert.NotNull(await db.GrowthCampaigns.FindAsync(campaign.Id));
    }

    [Fact]
    public async Task An_unknown_goal_is_rejected_and_creates_nothing()
    {
        var (db, svc, _) = Setup();
        using var _db = db;

        await Assert.ThrowsAsync<AppException>(
            () => svc.CreateAsync(new CreateCampaignRequest("go-viral", null, 9, null, null), 1));
        Assert.Empty(await db.GrowthCampaigns.ToListAsync());
    }

    [Fact]
    public async Task Deleting_a_campaign_removes_its_content()
    {
        var (db, svc, _) = Setup();
        using var _db = db;
        var campaign = await svc.CreateAsync(new CreateCampaignRequest("new-arrival", null, 9, null, null), 1);

        await svc.DeleteAsync(campaign.Id);

        Assert.Empty(await db.GrowthCampaigns.ToListAsync());
        Assert.Empty(await db.GrowthContents.Where(c => c.CampaignId == campaign.Id).ToListAsync());
    }

    private sealed class CountingAi : IAiService
    {
        public int Calls { get; private set; }
        public int? FailOnCall { get; set; }
        public string LastUser { get; private set; } = "";
        public bool Enabled => true;
        public long EstimateCostMicros(AiCompletion completion) => 0;

        public Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
        {
            Calls++;
            LastUser = prompt.User;
            if (FailOnCall == Calls) throw new AppException("Out of credits.", 402);
            return Task.FromResult(new AiCompletion("SUBJECT: Hi\n\nGenerated copy.", 100, 40, "test-model"));
        }
    }

    private sealed class PassThroughCredits(IAiService ai) : IAiCreditService
    {
        public async Task<T> MeterAsync<T>(string feature, Func<IAiService, Task<(T, AiCompletion)>> action, CancellationToken ct = default)
        {
            var (result, _) = await action(ai);
            return result;
        }

        public Task<AiBalanceDto> GetBalanceAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiUsageDto>> GetUsageAsync(int take = 50, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AiCreditPack?> GetPackAsync(int packId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> TopUpAsync(int packId, string reference, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
