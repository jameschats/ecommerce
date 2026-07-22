using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Features.Contact;
using ecomm.api.Features.Notifications;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// Storefront contact form (C0). Before this, submissions were silently discarded — so the
/// tests that matter most are "a valid message is actually persisted" and "the merchant is told".
/// </summary>
public class ContactTests
{
    private static (EcommerceDbContext db, ContactService svc, ContactTestFeed feed) Setup(long tenantId = 1)
    {
        var db = TestDb.New(tenantId);
        var feed = new ContactTestFeed();
        return (db, new ContactService(db, feed), feed);
    }

    private static SubmitContactRequest Valid(string? website = null) =>
        new("Priya", "priya@example.com", "9876543210", "Saree availability",
            "Do you have this in blue?", "https://acme.test/product/silk-saree", website);

    [Fact]
    public async Task Valid_message_is_persisted_and_notifies_the_merchant()
    {
        var (db, svc, feed) = Setup();
        using var _ = db;

        await svc.SubmitAsync(Valid());

        var saved = await db.ContactMessages.SingleAsync();
        Assert.Equal("Priya", saved.Name);
        Assert.Equal("priya@example.com", saved.Email);
        Assert.Equal("New", saved.Status);
        Assert.Equal("https://acme.test/product/silk-saree", saved.SourceUrl);

        Assert.Single(feed.AdminNotifications);
        Assert.Contains("Priya", feed.AdminNotifications[0].Title);
        Assert.Equal("/admin/messages", feed.AdminNotifications[0].LinkUrl);
    }

    [Fact]
    public async Task Honeypot_submission_is_silently_dropped()
    {
        var (db, svc, feed) = Setup();
        using var _ = db;

        // No exception — a bot must not be able to tell acceptance from rejection.
        await svc.SubmitAsync(Valid(website: "http://spam.example"));

        Assert.Empty(await db.ContactMessages.ToListAsync());
        Assert.Empty(feed.AdminNotifications);
    }

    [Theory]
    [InlineData("", "a@b.com", "hello")]
    [InlineData("Priya", "", "hello")]
    [InlineData("Priya", "a@b.com", "")]
    [InlineData("Priya", "not-an-email", "hello")]
    public async Task Incomplete_or_malformed_submissions_are_rejected(string name, string email, string body)
    {
        var (db, svc, _) = Setup();
        using var _db = db;

        await Assert.ThrowsAsync<AppException>(
            () => svc.SubmitAsync(new SubmitContactRequest(name, email, null, null, body, null, null)));
        Assert.Empty(await db.ContactMessages.ToListAsync());
    }

    [Fact]
    public async Task Overlong_input_is_truncated_rather_than_failing()
    {
        var (db, svc, _) = Setup();
        using var _ = db;

        await svc.SubmitAsync(new SubmitContactRequest(
            new string('n', 500), "priya@example.com", null, null, new string('b', 9000), null, null));

        var saved = await db.ContactMessages.SingleAsync();
        Assert.Equal(120, saved.Name.Length);
        Assert.Equal(4000, saved.Body.Length);
    }

    [Fact]
    public async Task Marking_handled_records_who_and_when_and_is_reversible()
    {
        var (db, svc, _) = Setup();
        using var _ = db;
        await svc.SubmitAsync(Valid());
        var id = (await db.ContactMessages.SingleAsync()).ContactMessageId;

        var handled = await svc.SetStatusAsync(id, "Handled", userId: 7);
        Assert.Equal("Handled", handled.Status);
        Assert.NotNull(handled.HandledAt);
        Assert.Equal(7, (await db.ContactMessages.SingleAsync()).HandledByUserId);
        Assert.Equal(0, await svc.NewCountAsync());

        var reopened = await svc.SetStatusAsync(id, "New", userId: 7);
        Assert.Equal("New", reopened.Status);
        Assert.Null(reopened.HandledAt);
        Assert.Null((await db.ContactMessages.SingleAsync()).HandledByUserId);
        Assert.Equal(1, await svc.NewCountAsync());
    }

    [Fact]
    public async Task Unknown_status_is_rejected()
    {
        var (db, svc, _) = Setup();
        using var _ = db;
        await svc.SubmitAsync(Valid());
        var id = (await db.ContactMessages.SingleAsync()).ContactMessageId;

        await Assert.ThrowsAsync<AppException>(() => svc.SetStatusAsync(id, "Spam", null));
    }

    [Fact]
    public async Task Listing_filters_by_status_and_is_newest_first()
    {
        var (db, svc, _) = Setup();
        using var _ = db;
        await svc.SubmitAsync(Valid());
        await svc.SubmitAsync(new SubmitContactRequest("Ravi", "ravi@example.com", null, null, "Second", null, null));

        var all = await svc.ListAsync(null, 1, 20);
        Assert.Equal(2, all.TotalCount);
        Assert.Equal("Ravi", all.Items.First().Name);      // newest first

        var first = all.Items.Last().ContactMessageId;
        await svc.SetStatusAsync(first, "Handled", null);

        Assert.Equal(1, (await svc.ListAsync("New", 1, 20)).TotalCount);
        Assert.Equal(1, (await svc.ListAsync("Handled", 1, 20)).TotalCount);
    }

}

/// <summary>Captures notifications instead of pushing them, so tests can assert the merchant was told.</summary>
public sealed class ContactTestFeed : INotificationFeedService
{
    public List<(string Type, string Title, string? Message, string? LinkUrl)> AdminNotifications { get; } = [];
    public List<(long UserId, string Type, string Title)> UserNotifications { get; } = [];

    public Task NotifyAdminsAsync(string type, string title, string? message, string? linkUrl, CancellationToken ct = default)
    {
        AdminNotifications.Add((type, title, message, linkUrl));
        return Task.CompletedTask;
    }

    public Task NotifyUserAsync(long userId, string type, string title, string? message, string? linkUrl, CancellationToken ct = default)
    {
        UserNotifications.Add((userId, type, title));
        return Task.CompletedTask;
    }

    public Task<List<NotificationDto>> ListAsync(long userId, bool isAdmin, int limit, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<int> UnreadCountAsync(long userId, bool isAdmin, CancellationToken ct = default) => throw new NotSupportedException();
    public Task MarkReadAsync(long userId, bool isAdmin, long id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task MarkAllReadAsync(long userId, bool isAdmin, CancellationToken ct = default) => throw new NotSupportedException();
}

/// <summary>
/// Records live pushes instead of opening a socket. Tests assert on the persisted rows — the push
/// is an accelerator, so what matters here is only that it's attempted for the right messages.
/// </summary>
public sealed class RecordingRealtime : IConversationRealtime
{
    public List<LiveMessageDto> Pushed { get; } = [];

    public Task MessageAsync(LiveMessageDto message, CancellationToken ct = default)
    {
        Pushed.Add(message);
        return Task.CompletedTask;
    }
}
