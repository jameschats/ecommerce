using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class AiCreditTests
{
    /// <summary>Fake provider: always enabled, returns a fixed completion with known token usage.</summary>
    private sealed class FakeAi(bool enabled = true) : IAiService
    {
        public bool Enabled => enabled;
        public long EstimateCostMicros(AiCompletion completion) => 0;
        public Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
            => Task.FromResult(new AiCompletion("ok", 10, 5, "test-model"));
    }

    private static AiCreditService NewService(EcommerceDbContext db, bool aiEnabled = true) =>
        new(db, new FakeAi(aiEnabled), new NullImageAiService(), new HttpContextAccessor());

    private static void SeedPlan(EcommerceDbContext db, int aiCredits)
    {
        db.Plans.Add(new Plan { PlanId = 1, Name = "Starter", Slug = "starter", AiCredits = aiCredits, IsActive = true });
        db.TenantSubscriptions.Add(new TenantSubscription { PlanId = 1, Status = "Trial", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    [Fact]
    public async Task Balance_seeds_from_plan_allowance_and_logs_a_grant()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db, aiCredits: 50);

        var dto = await NewService(db).GetBalanceAsync();

        Assert.Equal(50, dto.Balance);
        Assert.True(dto.Enabled);
        Assert.Contains(await db.AiUsageLogs.ToListAsync(), l => l.Feature == "grant" && l.Credits == 50);
    }

    [Fact]
    public async Task Meter_debits_the_action_cost_and_logs_usage()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db, aiCredits: 10);
        var svc = NewService(db);

        var text = await svc.MeterAsync(AiCreditPricing.ImproveText, async ai =>
        {
            var c = await ai.CompleteAsync(new AiPrompt("s", "u"));
            return (c.Text, c);
        });

        Assert.Equal("ok", text);
        Assert.Equal(9, (await svc.GetBalanceAsync()).Balance);   // 10 seeded − 1 (improve-text)
        var debit = Assert.Single(await db.AiUsageLogs.Where(l => l.Feature == AiCreditPricing.ImproveText).ToListAsync());
        Assert.Equal(-1, debit.Credits);
        Assert.Equal(15, debit.Tokens);   // 10 prompt + 5 completion
    }

    [Fact]
    public async Task Meter_throws_when_out_of_credits_and_does_not_debit()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db, aiCredits: 0);
        var svc = NewService(db);

        await Assert.ThrowsAsync<AppException>(() => svc.MeterAsync(AiCreditPricing.Page, async ai =>
        {
            var c = await ai.CompleteAsync(new AiPrompt("s", "u"));
            return (c.Text, c);
        }));

        Assert.Empty(await db.AiUsageLogs.Where(l => l.Feature == AiCreditPricing.Page).ToListAsync());
    }

    [Fact]
    public async Task Meter_throws_when_provider_disabled()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db, aiCredits: 100);
        var svc = NewService(db, aiEnabled: false);

        await Assert.ThrowsAsync<AppException>(() => svc.MeterAsync(AiCreditPricing.ImproveText,
            ai => Task.FromResult(("x", new AiCompletion("x", 1, 1, "m")))));
    }

    [Fact]
    public async Task TopUp_adds_the_pack_credits()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db, aiCredits: 5);
        db.AiCreditPacks.Add(new AiCreditPack { AiCreditPackId = 2, Name = "Growth", Credits = 600, PriceInr = 499m, IsActive = true });
        db.SaveChanges();
        var svc = NewService(db);

        var balance = await svc.TopUpAsync(2, "mock_ref", CancellationToken.None);

        Assert.Equal(605, balance);   // 5 seeded + 600
        Assert.Contains(await db.AiUsageLogs.ToListAsync(), l => l.Feature == "topup" && l.Credits == 600 && l.Model == "mock_ref");
    }
}
