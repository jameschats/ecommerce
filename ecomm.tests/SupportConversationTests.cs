using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// The generalised conversation model (C1). SupportTicket used to carry two-party booleans
/// (OpenedByPlatform / FromPlatform) that cannot express a third participant; these lock in the
/// Axis + AuthorType replacement, the V2-9 fields, and that the platform queue never sees
/// shopper threads.
/// </summary>
public class SupportConversationTests
{
    private static (EcommerceDbContext db, SupportService svc) Setup(long tenantId = 1)
    {
        var db = TestDb.New(tenantId);
        return (db, new SupportService(db, new FixedTenant(tenantId)));
    }

    [Fact]
    public async Task Merchant_ticket_is_on_the_platform_axis_and_gets_a_reference()
    {
        var (db, svc) = Setup();
        using var _ = db;

        var dto = await svc.CreateAsync("Payouts question", "When do payouts land?", userId: 4, default);

        var saved = await db.SupportTickets.SingleAsync();
        Assert.Equal(ConversationAxis.MerchantPlatform, saved.Axis);
        Assert.Equal($"TKT-{saved.CreatedAt:yyyy}-{saved.SupportTicketId:D5}", saved.Reference);
        Assert.Equal("Normal", saved.Priority);
        Assert.Equal(ConversationAxis.MerchantPlatform, dto.Axis);

        var msg = await db.SupportMessages.SingleAsync();
        Assert.Equal(MessageAuthorType.Merchant, msg.AuthorType);
        Assert.False(msg.FromPlatform);   // legacy column still written for rollback safety
    }

    [Fact]
    public async Task Platform_reply_is_authored_as_platform_and_stamps_first_response()
    {
        var (db, svc) = Setup();
        using var _ = db;
        await svc.CreateAsync("Help", "Something broke", 4, default);
        var id = (await db.SupportTickets.SingleAsync()).SupportTicketId;

        await svc.AdminReplyAsync(id, "Looking into it", adminUserId: 1, isInternal: false, default);

        var ticket = await db.SupportTickets.SingleAsync();
        Assert.NotNull(ticket.FirstResponseAt);
        Assert.Equal("Pending", ticket.Status);

        var reply = await db.SupportMessages.OrderBy(m => m.SupportMessageId).LastAsync();
        Assert.Equal(MessageAuthorType.Platform, reply.AuthorType);
        Assert.True(reply.FromPlatform);

        // A second reply must not move the first-response stamp.
        var firstAt = ticket.FirstResponseAt;
        await svc.AdminReplyAsync(id, "Still looking", 1, false, default);
        Assert.Equal(firstAt, (await db.SupportTickets.SingleAsync()).FirstResponseAt);
    }

    [Fact]
    public async Task Internal_note_does_not_count_as_a_response_and_stays_hidden_from_the_merchant()
    {
        var (db, svc) = Setup();
        using var _ = db;
        await svc.CreateAsync("Help", "Something broke", 4, default);
        var id = (await db.SupportTickets.SingleAsync()).SupportTicketId;

        await svc.AdminReplyAsync(id, "Probably their DNS", adminUserId: 1, isInternal: true, default);

        var ticket = await db.SupportTickets.SingleAsync();
        Assert.Null(ticket.FirstResponseAt);
        Assert.Equal("Open", ticket.Status);

        var merchantView = await svc.ThreadAsync(id, default);
        Assert.DoesNotContain(merchantView.Messages, m => m.Body.Contains("DNS"));

        var platformView = await svc.AdminThreadAsync(id, default);
        Assert.Contains(platformView.Messages, m => m.Body.Contains("DNS"));
    }

    [Fact]
    public async Task Closing_stamps_resolved_and_a_merchant_reply_reopens_and_clears_it()
    {
        var (db, svc) = Setup();
        using var _ = db;
        await svc.CreateAsync("Help", "Something broke", 4, default);
        var id = (await db.SupportTickets.SingleAsync()).SupportTicketId;

        await svc.SetStatusAsync(id, "Closed", adminUserId: 1, default);
        Assert.NotNull((await db.SupportTickets.SingleAsync()).ResolvedAt);

        await svc.ReplyAsync(id, "Actually, still broken", userId: 4, default);

        var ticket = await db.SupportTickets.SingleAsync();
        Assert.Equal("Open", ticket.Status);
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public async Task Triage_sets_priority_category_and_assignee_and_validates_priority()
    {
        var (db, svc) = Setup();
        using var _ = db;
        await svc.CreateAsync("Help", "Something broke", 4, default);
        var id = (await db.SupportTickets.SingleAsync()).SupportTicketId;

        var dto = await svc.TriageAsync(id, "Urgent", "Billing", assignedToUserId: 9, adminUserId: 1, default);
        Assert.Equal("Urgent", dto.Priority);
        Assert.Equal("Billing", dto.Category);
        Assert.Equal(9, dto.AssignedToUserId);

        // Omitted fields are left alone.
        var again = await svc.TriageAsync(id, null, null, null, 1, default);
        Assert.Equal("Urgent", again.Priority);
        Assert.Equal("Billing", again.Category);

        await Assert.ThrowsAsync<AppException>(() => svc.TriageAsync(id, "Whenever", null, null, 1, default));
    }

    [Fact]
    public async Task Shopper_threads_never_appear_in_the_merchant_or_platform_ticket_lists()
    {
        var (db, svc) = Setup();
        using var _ = db;
        await svc.CreateAsync("Payouts question", "When do payouts land?", 4, default);

        // A shopper↔merchant conversation on the same tenant.
        db.SupportTickets.Add(new SupportTicket
        {
            Axis = ConversationAxis.ShopperMerchant, Subject = "Where is my order?",
            Status = "Open", ShopperEmail = "priya@example.com", CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.Single(await svc.MyTicketsAsync(default));      // merchant's own platform tickets
        Assert.Single(await svc.QueueAsync(null, default));    // platform queue
        Assert.Equal(2, await db.SupportTickets.CountAsync()); // both rows exist
    }
}
