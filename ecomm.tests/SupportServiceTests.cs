using ecomm.api.Data.Context;
using ecomm.api.Features.Support;
using Xunit;

namespace ecomm.tests;

public class SupportServiceTests
{
    // Service + context share the tenant instance so AdminReply's BeginScope drives the auto-stamp.
    private static (EcommerceDbContext db, SupportService svc, FixedTenant tenant) Build(long tenantId)
    {
        var tenant = new FixedTenant(tenantId);
        var db = TestDb.ForDatabase(Guid.NewGuid().ToString(), tenant);
        return (db, new SupportService(db, tenant, new RecordingRealtime()), tenant);
    }

    [Fact]
    public async Task Merchant_thread_hides_internal_notes_admin_thread_shows_them()
    {
        var (db, svc, _) = Build(2);
        using (db)
        {
            var t = await svc.CreateAsync("Help", "Please help", userId: 5, default);
            Assert.Equal("Open", t.Status);
            Assert.Single(await svc.MyTicketsAsync(default));

            await svc.AdminReplyAsync(t.Id, "We're on it", adminUserId: 1, isInternal: false, default);
            await svc.AdminReplyAsync(t.Id, "internal only", adminUserId: 1, isInternal: true, default);

            var merchant = await svc.ThreadAsync(t.Id, default);
            Assert.Equal(2, merchant.Messages.Count);                       // first + public reply
            Assert.DoesNotContain(merchant.Messages, m => m.IsInternalNote);

            var admin = await svc.AdminThreadAsync(t.Id, default);
            Assert.Equal(3, admin.Messages.Count);                          // + internal note
            Assert.Equal("Pending", admin.Ticket.Status);                   // a public platform reply awaits the merchant
        }
    }

    [Fact]
    public async Task Merchant_cannot_see_another_tenants_tickets()
    {
        var (db, svc, tenant) = Build(2);
        using (db)
        {
            await svc.CreateAsync("T2 ticket", "msg", userId: 5, default);
            using (tenant.BeginScope(3))
                Assert.Empty(await svc.MyTicketsAsync(default));            // tenant 3 sees nothing
        }
    }

    [Fact]
    public async Task Merchant_reply_reopens_a_closed_ticket()
    {
        var (db, svc, _) = Build(2);
        using (db)
        {
            var t = await svc.CreateAsync("Help", "msg", userId: 5, default);
            await svc.SetStatusAsync(t.Id, "Closed", adminUserId: 1, default);
            await svc.ReplyAsync(t.Id, "still broken", userId: 5, default);

            Assert.Equal("Open", (await svc.ThreadAsync(t.Id, default)).Ticket.Status);
        }
    }
}
