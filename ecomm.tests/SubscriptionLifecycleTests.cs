using ecomm.api.Data.Entities;
using ecomm.api.Features.Payments;
using ecomm.api.Features.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

public class SubscriptionLifecycleTests
{
    // Provider defaults to Mock, so the factory never builds an HttpClient.
    private sealed class StubHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new NotImplementedException();
    }

    private static PlatformPaymentGatewayFactory Gateways(ecomm.api.Data.Context.EcommerceDbContext db) =>
        new(new StubHttpFactory(), Options.Create(new PaymentOptions()), db,
            Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create("ecomm.tests"));

    private static (ecomm.api.Data.Context.EcommerceDbContext db, SubscriptionService svc) NewSvc(long tenantId = 1)
    {
        // Service + context share the tenant instance so RecordCharge's BeginScope drives the auto-stamp.
        var tenant = new FixedTenant(tenantId);
        var db = TestDb.ForDatabase(Guid.NewGuid().ToString(), tenant);
        return (db, new SubscriptionService(db, Gateways(db), tenant));
    }

    [Fact]
    public async Task Ended_period_goes_pastdue_then_suspends_after_grace()
    {
        var (db, svc) = NewSvc();
        db.Tenants.Add(new Tenant { TenantId = 1, Name = "A", Slug = "a", IsActive = true });
        db.TenantSubscriptions.Add(new TenantSubscription
        {
            TenantId = 1, PlanId = 1, Status = SubscriptionService.Active,
            CurrentPeriodEnd = new DateTime(2026, 1, 1),
        });
        await db.SaveChangesAsync();

        // Day after period end → PastDue + 3-day grace
        var afterEnd = new DateTime(2026, 1, 2);
        Assert.Equal(1, await svc.RunLifecycleSweepAsync(afterEnd, graceDays: 3, default));
        var sub = await db.TenantSubscriptions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(SubscriptionService.PastDue, sub.Status);
        Assert.Equal(afterEnd.AddDays(3), sub.GraceEndsAt);

        // Within grace → no change
        Assert.Equal(0, await svc.RunLifecycleSweepAsync(afterEnd.AddDays(1), 3, default));

        // Past grace → Suspended + tenant.SuspendedAt set (store will 404)
        Assert.Equal(1, await svc.RunLifecycleSweepAsync(afterEnd.AddDays(4), 3, default));
        sub = await db.TenantSubscriptions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(SubscriptionService.Suspended, sub.Status);
        Assert.NotNull((await db.Tenants.SingleAsync(t => t.TenantId == 1)).SuspendedAt);
    }

    [Fact]
    public async Task Billing_history_is_newest_first_and_maps_fields()
    {
        var (db, svc) = NewSvc(tenantId: 1);
        db.TenantBillingHistory.Add(new TenantBillingHistory { Amount = 500m, Status = "Paid", BilledAt = new DateTime(2026, 1, 1), RazorpayPaymentId = "pay_old", PeriodStart = new DateTime(2026, 1, 1), PeriodEnd = new DateTime(2026, 2, 1) });
        db.TenantBillingHistory.Add(new TenantBillingHistory { Amount = 750m, Status = "Paid", BilledAt = new DateTime(2026, 2, 1), RazorpayPaymentId = "pay_new" });
        await db.SaveChangesAsync();

        var history = await svc.GetBillingHistoryAsync(default);

        Assert.Equal(2, history.Count);
        Assert.Equal("pay_new", history[0].Reference);        // newest first
        Assert.Equal(750m, history[0].Amount);
        Assert.Equal("pay_old", history[1].Reference);
        Assert.Equal(new DateTime(2026, 2, 1), history[1].PeriodEnd);
    }

    [Fact]
    public async Task Charge_is_idempotent_and_reactivates_a_suspended_store()
    {
        var (db, svc) = NewSvc();
        db.Tenants.Add(new Tenant { TenantId = 1, Name = "A", Slug = "a", IsActive = true, SuspendedAt = new DateTime(2026, 1, 5) });
        db.TenantSubscriptions.Add(new TenantSubscription { TenantId = 1, PlanId = 1, Status = SubscriptionService.Suspended });
        await db.SaveChangesAsync();

        var cmd = new RecordChargeCommand(1, 999m, "pay_ABC", "sub_1", new DateTime(2026, 2, 1), new DateTime(2026, 3, 1));

        Assert.True(await svc.RecordChargeAsync(cmd, default));    // first: recorded
        Assert.False(await svc.RecordChargeAsync(cmd, default));   // duplicate: ignored

        Assert.Equal(1, await db.TenantBillingHistory.IgnoreQueryFilters().CountAsync());   // only one billing row
        var sub = await db.TenantSubscriptions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(SubscriptionService.Active, sub.Status);
        Assert.Null(sub.GraceEndsAt);
        Assert.Null((await db.Tenants.SingleAsync(t => t.TenantId == 1)).SuspendedAt);       // reactivated
    }

    [Fact]
    public async Task Checkout_confirm_records_charge_activates_plan_and_is_idempotent()
    {
        var (db, svc) = NewSvc();
        using (db)
        {
            db.Tenants.Add(new Tenant { TenantId = 1, Name = "Acme", Code = "acme", IsActive = true, CreatedAt = DateTime.UtcNow });
            db.Plans.Add(new Plan { PlanId = 1, Name = "Pro", Slug = "pro", MonthlyPrice = 999, IsActive = true });
            await db.SaveChangesAsync();

            var session = await svc.StartCheckoutAsync(1, default);
            Assert.Equal(999m, session.Amount);
            Assert.Equal("Mock", session.Provider);

            var cmd = new ConfirmCheckoutCommand(1, session.GatewayOrderId, "pay_test_1", "sig");
            var sub = await svc.ConfirmCheckoutAsync(cmd, default);

            Assert.Equal(SubscriptionService.Active, sub.Status);
            Assert.Equal(1, sub.PlanId);
            var charge = await db.TenantBillingHistory.SingleAsync();
            Assert.Equal("Paid", charge.Status);
            Assert.Equal(999m, charge.Amount);          // amount comes from the plan, not the client

            await svc.ConfirmCheckoutAsync(cmd, default);                 // webhook/duplicate delivery
            Assert.Equal(1, await db.TenantBillingHistory.CountAsync());  // still one charge
        }
    }

    [Fact]
    public async Task Intro_price_applies_for_the_first_cycles_then_reverts()
    {
        var (db, svc) = NewSvc();
        using (db)
        {
            db.Tenants.Add(new Tenant { TenantId = 1, Name = "Acme", Code = "acme", IsActive = true, CreatedAt = DateTime.UtcNow });
            // ₹20/mo for the first 2 months, then ₹1999 (a 99% intro offer).
            db.Plans.Add(new Plan { PlanId = 1, Name = "Pro", Slug = "pro", MonthlyPrice = 1999, IsActive = true, IntroPriceInr = 20, IntroMonths = 2 });
            await db.SaveChangesAsync();

            // Cycle 1 + 2 bill at the intro price.
            Assert.Equal(20m, (await svc.StartCheckoutAsync(1, default)).Amount);
            await svc.ConfirmCheckoutAsync(new ConfirmCheckoutCommand(1, "order_1", "pay_1", "sig"), default);
            Assert.Equal(20m, (await svc.StartCheckoutAsync(1, default)).Amount);
            await svc.ConfirmCheckoutAsync(new ConfirmCheckoutCommand(1, "order_2", "pay_2", "sig"), default);

            // Intro exhausted -> standard price.
            Assert.Equal(1999m, (await svc.StartCheckoutAsync(1, default)).Amount);

            var charged = await db.TenantBillingHistory.OrderBy(b => b.TenantBillingHistoryId).Select(b => b.Amount).ToListAsync();
            Assert.Equal(new[] { 20m, 20m }, charged);   // recorded at the intro price, not the list price
        }
    }

    [Fact]
    public async Task Expired_campaign_closes_the_offer_to_new_joiners_only()
    {
        // A merchant who has never paid gets the standard price once the campaign has ended.
        var (db1, svc1) = NewSvc();
        using (db1)
        {
            db1.Tenants.Add(new Tenant { TenantId = 1, Name = "New", Code = "new", IsActive = true, CreatedAt = DateTime.UtcNow });
            db1.Plans.Add(new Plan { PlanId = 1, Name = "Pro", Slug = "pro", MonthlyPrice = 1999, IsActive = true, IntroPriceInr = 20, IntroMonths = 3, IntroEndsAt = DateTime.UtcNow.AddDays(-1) });
            await db1.SaveChangesAsync();

            Assert.Equal(1999m, (await svc1.StartCheckoutAsync(1, default)).Amount);
        }

        // A merchant already on the offer keeps it for the rest of their cycles, deadline or not.
        var (db2, svc2) = NewSvc();
        using (db2)
        {
            db2.Tenants.Add(new Tenant { TenantId = 1, Name = "Existing", Code = "ex", IsActive = true, CreatedAt = DateTime.UtcNow });
            db2.Plans.Add(new Plan { PlanId = 1, Name = "Pro", Slug = "pro", MonthlyPrice = 1999, IsActive = true, IntroPriceInr = 20, IntroMonths = 3, IntroEndsAt = DateTime.UtcNow.AddDays(-1) });
            await db2.SaveChangesAsync();
            await svc2.ConfirmCheckoutAsync(new ConfirmCheckoutCommand(1, "order_0", "pay_0", "sig"), default);   // 1 cycle already paid

            Assert.Equal(20m, (await svc2.StartCheckoutAsync(1, default)).Amount);
        }
    }

    [Fact]
    public async Task Checkout_rejects_a_free_plan()
    {
        var (db, svc) = NewSvc();
        using (db)
        {
            db.Plans.Add(new Plan { PlanId = 2, Name = "Free", Slug = "free", MonthlyPrice = 0, IsActive = true });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<ecomm.api.Common.Exceptions.AppException>(() => svc.StartCheckoutAsync(2, default));
        }
    }
}
