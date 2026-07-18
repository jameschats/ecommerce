using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.SuperAdmin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

public class SuperAdminTenantAdminTests
{
    // The methods under test never mint a token → a throwing stub is enough.
    private sealed class StubJwt : IJwtTokenService
    {
        public (string, DateTime) CreateAccessToken(User u, IEnumerable<string> roles, IEnumerable<string> perms) => throw new NotImplementedException();
        public (string, DateTime) CreateImpersonationToken(User u, IEnumerable<string> roles, string mode, long by, int minutes = 30) => throw new NotImplementedException();
        public (string, string, DateTime) CreateRefreshToken() => throw new NotImplementedException();
        public string HashRefreshToken(string raw) => throw new NotImplementedException();
    }

    // Service + context must SHARE the tenant instance so GrantCredits' BeginScope affects the auto-stamp.
    private static (EcommerceDbContext db, SuperAdminService svc) Build(long contextTenant)
    {
        var tenant = new FixedTenant(contextTenant);
        var db = TestDb.ForDatabase(Guid.NewGuid().ToString(), tenant);
        var svc = new SuperAdminService(db, new StubJwt(), Options.Create(new TenancyOptions { BaseDomain = "wavcommerce.online" }), tenant);
        return (db, svc);
    }

    private static void SeedTenant(EcommerceDbContext db, int id = 2, int? planId = null)
        => db.Tenants.Add(new Tenant { TenantId = id, Name = "Acme", Code = "acme", IsActive = true, CreatedAt = DateTime.UtcNow, PlanId = planId });

    [Fact]
    public async Task ChangePlan_updates_tenant_and_subscription()
    {
        var (db, svc) = Build(2);
        using (db)
        {
            SeedTenant(db, planId: 1);
            db.Plans.Add(new Plan { PlanId = 1, Name = "Basic", Slug = "basic", MonthlyPrice = 0 });
            db.Plans.Add(new Plan { PlanId = 2, Name = "Pro", Slug = "pro", MonthlyPrice = 999 });
            db.TenantSubscriptions.Add(new TenantSubscription { TenantId = 2, PlanId = 1, Status = "Trial", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            await svc.ChangePlanAsync(2, 2, adminUserId: 1, default);

            Assert.Equal(2, (await db.Tenants.FirstAsync(x => x.TenantId == 2)).PlanId);
            Assert.Equal(2, (await db.TenantSubscriptions.IgnoreQueryFilters().FirstAsync(x => x.TenantId == 2)).PlanId);
        }
    }

    [Fact]
    public async Task Offboard_then_reactivate_toggles_state()
    {
        var (db, svc) = Build(2);
        using (db)
        {
            SeedTenant(db);
            await db.SaveChangesAsync();

            await svc.OffboardAsync(2, 1, default);
            var t = await db.Tenants.FirstAsync(x => x.TenantId == 2);
            Assert.False(t.IsActive);
            Assert.NotNull(t.OffboardedAt);

            await svc.SetActiveAsync(2, true, 1, default);
            t = await db.Tenants.FirstAsync(x => x.TenantId == 2);
            Assert.True(t.IsActive);
            Assert.Null(t.OffboardedAt);   // reactivate clears off-board
        }
    }

    [Fact]
    public async Task Notes_append_and_tags_normalize()
    {
        var (db, svc) = Build(2);
        using (db)
        {
            SeedTenant(db);
            await db.SaveChangesAsync();

            await svc.AddNoteAsync(2, "  called merchant  ", 1, default);
            await svc.SetTagsAsync(2, "vip, wholesale, vip", 1, default);

            Assert.Equal("called merchant", (await db.TenantNotes.FirstAsync()).Note);
            Assert.Equal("vip,wholesale", (await db.Tenants.FirstAsync(x => x.TenantId == 2)).PlatformTags);
        }
    }

    [Fact]
    public async Task CreatePlan_then_update_persists()
    {
        var (db, svc) = Build(1);
        using (db)
        {
            var created = await svc.CreatePlanAsync(new PlanUpsert("Growth Plan", null, 499, 500, 1000, 300, null, true, 2), 1, default);
            Assert.Equal("growth-plan", created.Slug);   // auto-slugified

            await svc.UpdatePlanAsync(created.PlanId, new PlanUpsert("Growth Plan", "growth-plan", 599, null, null, 400, null, false, 2), 1, default);
            var plan = await db.Plans.FirstAsync(p => p.PlanId == created.PlanId);
            Assert.Equal(599, plan.MonthlyPrice);
            Assert.Null(plan.MaxProducts);   // blank = unlimited
            Assert.False(plan.IsActive);
        }
    }

    [Fact]
    public async Task RecordManualPayment_writes_paid_charge_and_activates_subscription()
    {
        var (db, svc) = Build(1);   // super-admin in tenant-1 context records a payment for tenant 2
        using (db)
        {
            SeedTenant(db, id: 2);
            db.Plans.Add(new Plan { PlanId = 3, Name = "Pro", Slug = "pro", MonthlyPrice = 999 });
            await db.SaveChangesAsync();

            await svc.RecordManualPaymentAsync(2, 3, 999m, "manual-1", adminUserId: 1, default);

            var charge = await db.TenantBillingHistory.IgnoreQueryFilters().FirstAsync(b => b.TenantId == 2);
            Assert.Equal("Paid", charge.Status);
            Assert.Equal(999m, charge.Amount);

            var sub = await db.TenantSubscriptions.IgnoreQueryFilters().FirstAsync(s => s.TenantId == 2);
            Assert.Equal("Active", sub.Status);
            Assert.Equal(3, sub.PlanId);
            Assert.NotNull(sub.CurrentPeriodEnd);
            Assert.Equal(3, (await db.Tenants.FirstAsync(t => t.TenantId == 2)).PlanId);
        }
    }

    [Fact]
    public async Task GrantCredits_lands_on_target_tenant_and_clamps_at_zero()
    {
        var (db, svc) = Build(1);   // super-admin runs in tenant-1 context, grants to tenant 2
        using (db)
        {
            SeedTenant(db, id: 2);
            await db.SaveChangesAsync();

            await svc.GrantCreditsAsync(2, 50, "goodwill", adminUserId: 1, default);
            var credit = await db.TenantAiCredits.IgnoreQueryFilters().FirstAsync(c => c.TenantId == 2);
            Assert.Equal(50, credit.Balance);
            Assert.True(await db.AiUsageLogs.IgnoreQueryFilters().AnyAsync(l => l.TenantId == 2 && l.Feature == "grant" && l.Credits == 50));

            await svc.GrantCreditsAsync(2, -100, "clawback", adminUserId: 1, default);   // over-deduct
            credit = await db.TenantAiCredits.IgnoreQueryFilters().FirstAsync(c => c.TenantId == 2);
            Assert.Equal(0, credit.Balance);   // never negative
        }
    }
}
