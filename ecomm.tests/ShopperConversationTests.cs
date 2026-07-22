using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Notifications;
using ecomm.api.Features.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// Shopper-to-merchant conversations (C1). The access rules carry the weight here: an anonymous
/// shopper reaches exactly one thread via a signed token, and a signed-in shopper reaches only
/// their own - so most of these tests are about what must NOT be reachable.
/// </summary>
public class ShopperConversationTests
{
    private static (EcommerceDbContext db, ShopperConversationService svc, ContactTestFeed feed, RecordingEmail email) Setup(long tenantId = 1)
    {
        var db = TestDb.New(tenantId);
        var feed = new ContactTestFeed();
        var email = new RecordingEmail();
        var svc = new ShopperConversationService(
            db, DataProtectionProvider.Create("ecomm-tests"), feed, email,
            new FixedTenant(tenantId),
            Options.Create(new TenancyOptions { BaseDomain = "wavcommerce.online", DefaultTenantId = 1 }));
        return (db, svc, feed, email);
    }

    private static StartConversationRequest Start(string? email = "priya@example.com", long? orderId = null, long? productId = null) =>
        new("Where is my order?", "It's been a week.", email, "Priya", orderId, productId);

    [Fact]
    public async Task Anonymous_shopper_starts_a_thread_on_the_shopper_axis_and_gets_a_reply_token()
    {
        var (db, svc, feed, _mail) = Setup();
        using var _ = db;

        var thread = await svc.StartAsync(Start(), shopperUserId: null);

        var saved = await db.SupportTickets.SingleAsync();
        Assert.Equal(ConversationAxis.ShopperMerchant, saved.Axis);
        Assert.Equal("priya@example.com", saved.ShopperEmail);
        Assert.Null(saved.ShopperUserId);
        Assert.StartsWith("TKT-", saved.Reference);

        Assert.Equal(MessageAuthorType.Shopper, (await db.SupportMessages.SingleAsync()).AuthorType);
        Assert.False(string.IsNullOrWhiteSpace(thread.ReplyToken));
        Assert.Single(feed.AdminNotifications);
    }

    [Fact]
    public async Task A_valid_token_opens_exactly_its_own_thread()
    {
        var (db, svc, _, _mail) = Setup();
        using var _db = db;
        var a = await svc.StartAsync(Start(), null);
        var b = await svc.StartAsync(Start("ravi@example.com"), null);

        var opened = await svc.GetByTokenAsync(a.ReplyToken!);
        Assert.Equal(a.Conversation.Id, opened.Conversation.Id);
        Assert.NotEqual(b.Conversation.Id, opened.Conversation.Id);
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("")]
    public async Task A_forged_token_is_rejected(string token)
    {
        var (db, svc, _, _mail) = Setup();
        using var _db = db;
        await svc.StartAsync(Start(), null);

        await Assert.ThrowsAsync<AppException>(() => svc.GetByTokenAsync(token));
    }

    [Fact]
    public async Task A_token_from_a_different_key_ring_is_rejected()
    {
        var (db, svc, _, _mail) = Setup();
        using var _db = db;
        var thread = await svc.StartAsync(Start(), null);

        // Same conversation id, but signed by someone else's keys.
        var forged = DataProtectionProvider.Create("attacker")
            .CreateProtector(ShopperConversationService.ProtectorPurpose)
            .Protect(thread.Conversation.Id.ToString());

        await Assert.ThrowsAsync<AppException>(() => svc.GetByTokenAsync(forged));
    }

    [Fact]
    public async Task A_signed_in_shopper_cannot_read_or_reply_to_someone_elses_thread()
    {
        var (db, svc, _, _mail) = Setup();
        using var _db = db;
        db.Users.Add(new User { UserId = 5, Email = "priya@example.com", NormalizedEmail = "PRIYA@EXAMPLE.COM", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Users.Add(new User { UserId = 6, Email = "ravi@example.com", NormalizedEmail = "RAVI@EXAMPLE.COM", IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var mine = await svc.StartAsync(Start(), shopperUserId: 5);

        await Assert.ThrowsAsync<AppException>(() => svc.GetForShopperAsync(mine.Conversation.Id, shopperUserId: 6));
        await Assert.ThrowsAsync<AppException>(() => svc.ReplyAsShopperAsync(mine.Conversation.Id, "hello", shopperUserId: 6, token: null));
        Assert.Empty(await svc.MineAsync(6));
        Assert.Single(await svc.MineAsync(5));
    }

    [Fact]
    public async Task An_unauthenticated_reply_without_a_token_is_refused()
    {
        var (db, svc, _, _mail) = Setup();
        using var _db = db;
        var thread = await svc.StartAsync(Start(), null);

        await Assert.ThrowsAsync<AppException>(
            () => svc.ReplyAsShopperAsync(thread.Conversation.Id, "hello", shopperUserId: null, token: null));
    }

    [Fact]
    public async Task An_order_the_shopper_does_not_own_is_not_linked()
    {
        var (db, svc, _, _mail) = Setup();
        using var _db = db;
        db.Users.Add(new User { UserId = 5, Email = "priya@example.com", NormalizedEmail = "P", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Orders.Add(new Order { OrderId = 77, TenantId = 1, UserId = 999, OrderNumber = "ORD-X", Status = "Confirmed", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var thread = await svc.StartAsync(Start(orderId: 77), shopperUserId: 5);

        Assert.Null(thread.Conversation.OrderId);   // silently dropped, not trusted
    }

    [Fact]
    public async Task Merchant_reply_stamps_first_response_and_awaits_the_shopper()
    {
        var (db, svc, _, _mail) = Setup();
        using var _db = db;
        var thread = await svc.StartAsync(Start(), null);

        await svc.ReplyAsMerchantAsync(thread.Conversation.Id, "Out for delivery tomorrow.", merchantUserId: 1);

        var convo = await db.SupportTickets.SingleAsync();
        Assert.NotNull(convo.FirstResponseAt);
        Assert.Equal("Pending", convo.Status);
        Assert.Equal(MessageAuthorType.Merchant, (await db.SupportMessages.OrderBy(m => m.SupportMessageId).LastAsync()).AuthorType);

        // Shopper replying via the token reopens it.
        await svc.ReplyAsShopperAsync(thread.Conversation.Id, "Still not here", null, thread.ReplyToken);
        Assert.Equal("Open", (await db.SupportTickets.SingleAsync()).Status);
    }

    [Fact]
    public async Task Merchant_inbox_shows_shopper_threads_only()
    {
        var (db, svc, _, _mail) = Setup();
        using var _db = db;
        await svc.StartAsync(Start(), null);

        // A merchant-to-platform ticket on the same tenant must not appear.
        db.SupportTickets.Add(new SupportTicket
        {
            Axis = ConversationAxis.MerchantPlatform, Subject = "Payouts", Status = "Open", CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var inbox = await svc.InboxAsync(null, 1, 20);
        Assert.Equal(1, inbox.TotalCount);
        Assert.Equal("Where is my order?", inbox.Items.Single().Subject);
        Assert.Equal(1, await svc.OpenCountAsync());
    }

    [Fact]
    public async Task Internal_notes_are_never_shown_to_the_shopper()
    {
        var (db, svc, _, _mail) = Setup();
        using var _db = db;
        var thread = await svc.StartAsync(Start(), null);

        db.SupportMessages.Add(new SupportMessage
        {
            SupportTicketId = thread.Conversation.Id, AuthorType = MessageAuthorType.Merchant,
            IsInternalNote = true, Body = "known repeat complainer", CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var reopened = await svc.GetByTokenAsync(thread.ReplyToken!);
        Assert.DoesNotContain(reopened.Messages, m => m.Body.Contains("complainer"));
    }

    [Fact]
    public async Task A_message_without_a_usable_email_is_rejected()
    {
        var (db, svc, _, _mail) = Setup();
        using var _db = db;

        await Assert.ThrowsAsync<AppException>(() => svc.StartAsync(Start(email: null), null));
        await Assert.ThrowsAsync<AppException>(() => svc.StartAsync(Start(email: "nonsense"), null));
    }

    [Fact]
    public async Task An_anonymous_shopper_is_emailed_a_token_link_when_the_merchant_replies()
    {
        var (db, svc, _, mail) = Setup();
        using var _db = db;
        var thread = await svc.StartAsync(Start(), shopperUserId: null);

        await svc.ReplyAsMerchantAsync(thread.Conversation.Id, "It ships tomorrow.", merchantUserId: 1);

        var sent = Assert.Single(mail.Emails);
        Assert.Equal("ConversationReply", sent.Code);
        Assert.Equal("priya@example.com", sent.Recipient);
        // Without a login there is no account page to send them to — the token link is the only way back.
        Assert.Contains("/thread/", sent.Tokens["ThreadUrl"]);
        Assert.Contains("It ships tomorrow.", sent.Tokens["MessagePreview"]);
    }

    [Fact]
    public async Task A_signed_in_shopper_is_pointed_at_their_account_not_a_token_link()
    {
        var (db, svc, _, mail) = Setup();
        using var _db = db;
        db.Users.Add(new User { UserId = 5, Email = "priya@example.com", NormalizedEmail = "P", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Tenants.Add(new Tenant { TenantId = 1, Name = "Acme", Slug = "acme" });
        await db.SaveChangesAsync();

        var thread = await svc.StartAsync(Start(), shopperUserId: 5);
        await svc.ReplyAsMerchantAsync(thread.Conversation.Id, "On its way.", merchantUserId: 1);

        var sent = Assert.Single(mail.Emails);
        Assert.Contains("/account/conversations", sent.Tokens["ThreadUrl"]);
        Assert.DoesNotContain("/thread/", sent.Tokens["ThreadUrl"]);
    }
}

/// <summary>Captures templated sends so tests can assert what a shopper would actually receive.</summary>
public sealed class RecordingEmail : INotificationService
{
    public List<(string Code, string Recipient, IReadOnlyDictionary<string, string> Tokens)> Emails { get; } = [];

    public Task<bool> SendEmailAsync(string code, string toEmail, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default)
    {
        Emails.Add((code, toEmail, tokens));
        return Task.FromResult(true);
    }

    public Task<bool> SendSmsAsync(string code, string toPhone, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default)
        => Task.FromResult(true);
}
