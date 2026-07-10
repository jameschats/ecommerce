using ecomm.api.Data.Entities;
using ecomm.api.Features.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class SubscriptionLifecycleTests
{
    private static (ecomm.api.Data.Context.EcommerceDbContext db, SubscriptionService svc) NewSvc(long tenantId = 1)
    {
        var db = TestDb.New(tenantId);
        return (db, new SubscriptionService(db));
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
}
