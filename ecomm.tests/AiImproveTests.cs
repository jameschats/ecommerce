using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class AiImproveTests
{
    /// <summary>Fake provider returning a fixed response (with known token usage).</summary>
    private sealed class CannedAi(string response) : IAiService
    {
        public bool Enabled => true;
        public long EstimateCostMicros(AiCompletion completion) => 0;
        public Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
            => Task.FromResult(new AiCompletion(response, 8, 4, "test-model"));
    }

    private static (AiImproveService svc, EcommerceDbContext db) New(string cannedResponse, int credits = 100)
    {
        var db = TestDb.New(tenantId: 1);
        db.Plans.Add(new Plan { PlanId = 1, Name = "P", Slug = "p", AiCredits = credits, IsActive = true });
        db.TenantSubscriptions.Add(new TenantSubscription { PlanId = 1, Status = "Trial", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        var creditSvc = new AiCreditService(db, new CannedAi(cannedResponse), new HttpContextAccessor());
        return (new AiImproveService(creditSvc), db);
    }

    [Fact]
    public async Task Improve_strips_wrapping_quotes_and_debits_one_credit()
    {
        var (svc, db) = New("\"A crisp cotton tee, tailored for summer.\"");
        using var _d = db;

        var text = await svc.ImproveAsync("product-description", "old copy", "Cotton T-Shirt", CancellationToken.None);

        Assert.Equal("A crisp cotton tee, tailored for summer.", text);   // surrounding quotes removed
        Assert.Contains(await db.AiUsageLogs.ToListAsync(), l => l.Feature == AiCreditPricing.ImproveText && l.Credits == -1);
    }

    [Fact]
    public async Task Seo_parses_json_into_title_and_meta()
    {
        var (svc, db) = New("{\"title\":\"Cotton T-Shirt — Soft & Breathable\",\"metaDescription\":\"Shop our soft cotton tee.\"}");
        using var _d = db;

        var seo = await svc.SeoAsync("Cotton T-Shirt", "A soft tee", CancellationToken.None);

        Assert.Equal("Cotton T-Shirt — Soft & Breathable", seo.Title);
        Assert.Equal("Shop our soft cotton tee.", seo.MetaDescription);
        Assert.Contains(await db.AiUsageLogs.ToListAsync(), l => l.Feature == AiCreditPricing.Seo && l.Credits == -1);
    }

    [Fact]
    public async Task Seo_requires_a_product_name()
    {
        var (svc, db) = New("{}");
        using var _d = db;
        await Assert.ThrowsAsync<AppException>(() => svc.SeoAsync("  ", null, CancellationToken.None));
    }
}
