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
        return (db, new SupportService(db, new FixedTenant(tenantId), new RecordingRealtime()));
    }

    private static (EcommerceDbContext db, SupportService svc, RecordingRealtime live) SetupWithRealtime(long tenantId = 1)
    {
        var db = TestDb.New(tenantId);
        var live = new RecordingRealtime();
        return (db, new SupportService(db, new FixedTenant(tenantId), live), live);
    }

    [Fact]
    public async Task An_internal_note_is_never_pushed_live_but_a_real_reply_is()
    {
        var (db, svc, live) = SetupWithRealtime();
        using var _ = db;
        await svc.CreateAsync("Help", "Something broke", 4, default);
        var id = (await db.SupportTickets.SingleAsync()).SupportTicketId;
        live.Pushed.Clear();   // ignore the opening message

        await svc.AdminReplyAsync(id, "Probably their DNS", adminUserId: 1, isInternal: true, default);
        Assert.Empty(live.Pushed);

        await svc.AdminReplyAsync(id, "We're on it", adminUserId: 1, isInternal: false, default);
        var pushed = Assert.Single(live.Pushed);
        Assert.Equal("We're on it", pushed.Body);
        Assert.Equal(MessageAuthorType.Platform, pushed.AuthorType);
    }

    [Fact]
    public async Task A_pushed_message_is_saved_before_it_is_broadcast()
    {
        var (db, svc, live) = SetupWithRealtime();
        using var _ = db;
        await svc.CreateAsync("Help", "Something broke", 4, default);
        var id = (await db.SupportTickets.SingleAsync()).SupportTicketId;

        await svc.ReplyAsync(id, "Any update?", userId: 4, default);

        // The push carries a real database id, which only exists after the save.
        var pushed = live.Pushed.Last();
        Assert.True(pushed.MessageId > 0);
        Assert.Contains(await db.SupportMessages.ToListAsync(), m => m.SupportMessageId == pushed.MessageId);
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

        SeedAgent(db, 9);   // assignment now validates the assignee is a platform agent
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

        Assert.Single(await svc.MyTicketsAsync(default));                    // merchant's own platform tickets
        Assert.Single(await svc.QueueAsync(NoFilter, default));              // platform queue
        Assert.Equal(2, await db.SupportTickets.CountAsync());              // both rows exist
    }

    [Fact]
    public async Task Escalating_steps_up_the_tier_bumps_priority_and_logs_it()
    {
        var (db, svc) = Setup();
        using var _ = db;
        await svc.CreateAsync("Help", "Broken", 4, default);
        var id = (await db.SupportTickets.SingleAsync()).SupportTicketId;

        var l2 = await svc.EscalateAsync(id, null, adminUserId: 1, default);
        Assert.Equal("L2", l2.EscalationTier);
        Assert.Equal("High", l2.Priority);   // escalation lifts a Normal ticket to at least High

        var l3 = await svc.EscalateAsync(id, null, 1, default);
        Assert.Equal("L3", l3.EscalationTier);

        // Stays at L3 (no L4).
        Assert.Equal("L3", (await svc.EscalateAsync(id, null, 1, default)).EscalationTier);

        var thread = await svc.AdminThreadAsync(id, default);
        Assert.Contains(thread.Activity, a => a.Type == "escalated" && a.Detail.Contains("L1 → L2"));
    }

    [Fact]
    public async Task Assigning_to_a_non_agent_is_rejected()
    {
        var (db, svc) = Setup();
        using var _ = db;
        db.Users.Add(new User { UserId = 50, Email = "nobody@x.test", NormalizedEmail = "NOBODY@X.TEST", IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await svc.CreateAsync("Help", "Broken", 4, default);
        var id = (await db.SupportTickets.SingleAsync()).SupportTicketId;

        await Assert.ThrowsAsync<AppException>(() => svc.AssignAsync(id, 50, adminUserId: 1, default));
        Assert.Null((await db.SupportTickets.SingleAsync()).AssignedToUserId);
    }

    [Fact]
    public async Task Assigning_to_an_agent_records_the_name_and_logs_it()
    {
        var (db, svc) = Setup();
        using var _ = db;
        SeedAgent(db, 9);
        await svc.CreateAsync("Help", "Broken", 4, default);
        var id = (await db.SupportTickets.SingleAsync()).SupportTicketId;

        var dto = await svc.AssignAsync(id, 9, adminUserId: 1, default);
        Assert.Equal(9, dto.AssignedToUserId);

        var thread = await svc.AdminThreadAsync(id, default);
        Assert.Equal("Agent 9", thread.Ticket.AssignedToName);
        Assert.Contains(thread.Activity, a => a.Type == "assignee" && a.Detail.Contains("Agent 9"));
    }

    [Fact]
    public async Task Status_change_stamps_resolved_and_records_the_transition()
    {
        var (db, svc) = Setup();
        using var _ = db;
        await svc.CreateAsync("Help", "Broken", 4, default);
        var id = (await db.SupportTickets.SingleAsync()).SupportTicketId;

        await svc.SetStatusAsync(id, "Resolved", adminUserId: 1, default);
        var ticket = await db.SupportTickets.SingleAsync();
        Assert.Equal("Resolved", ticket.Status);
        Assert.NotNull(ticket.ResolvedAt);   // Resolved counts, not only Closed

        var thread = await svc.AdminThreadAsync(id, default);
        Assert.Contains(thread.Activity, a => a.Type == "status" && a.Detail.Contains("Resolved"));
    }

    [Fact]
    public async Task Tags_are_normalised_to_a_clean_deduped_list()
    {
        var (db, svc) = Setup();
        using var _ = db;
        await svc.CreateAsync("Help", "Broken", 4, default);
        var id = (await db.SupportTickets.SingleAsync()).SupportTicketId;

        var dto = await svc.SetTagsAsync(id, "  billing , URGENT, billing ,, refund ", adminUserId: 1, default);
        Assert.Equal("billing, URGENT, refund", dto.Tags);   // trimmed, de-duped, empties dropped
    }

    [Fact]
    public async Task The_queue_filters_by_tier_priority_and_assignment()
    {
        var (db, svc) = Setup();
        using var _ = db;
        SeedAgent(db, 9);
        await svc.CreateAsync("A", "x", 4, default);
        await svc.CreateAsync("B", "y", 4, default);
        var ids = await db.SupportTickets.OrderBy(t => t.SupportTicketId).Select(t => t.SupportTicketId).ToListAsync();

        await svc.EscalateAsync(ids[0], "L3", 1, default);   // A → L3, High
        await svc.AssignAsync(ids[1], 9, 1, default);         // B → agent 9

        Assert.Single(await svc.QueueAsync(new TicketQueueFilter(null, null, "L3", null, false), default));
        Assert.Single(await svc.QueueAsync(new TicketQueueFilter(null, null, null, 9, false), default));
        Assert.Single(await svc.QueueAsync(new TicketQueueFilter(null, null, null, null, true), default));   // unassigned = A
    }

    [Fact]
    public async Task The_merchant_thread_never_carries_the_activity_trail()
    {
        var (db, svc) = Setup();
        using var _ = db;
        await svc.CreateAsync("Help", "Broken", 4, default);
        var id = (await db.SupportTickets.SingleAsync()).SupportTicketId;
        await svc.EscalateAsync(id, null, 1, default);

        var merchantView = await svc.ThreadAsync(id, default);
        Assert.Empty(merchantView.Activity);   // internal-only

        var platformView = await svc.AdminThreadAsync(id, default);
        Assert.NotEmpty(platformView.Activity);
    }

    private static readonly TicketQueueFilter NoFilter = new(null, null, null, null, false);

    /// <summary>Makes a user a platform support agent (SuperAdmin role) so they can be assigned tickets.</summary>
    private static void SeedAgent(EcommerceDbContext db, long userId)
    {
        if (!db.Roles.Any(r => r.NormalizedName == "SUPERADMIN"))
            db.Roles.Add(new Role { RoleId = 99, Name = "SuperAdmin", NormalizedName = "SUPERADMIN" });
        db.Users.Add(new User { UserId = userId, Email = $"agent{userId}@platform.test", NormalizedEmail = $"AGENT{userId}@PLATFORM.TEST", FullName = $"Agent {userId}", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = 99 });
        db.SaveChanges();
    }
}
