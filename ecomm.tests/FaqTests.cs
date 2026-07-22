using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Faqs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// Per-tenant FAQs (C2). These were hardcoded in the Angular component until now, so the
/// behaviour worth locking in is that they're tenant-scoped, ordered, and that unpublished
/// entries never escape to the storefront — or, later, to the bot's retrieval corpus.
/// </summary>
public class FaqTests
{
    private static (EcommerceDbContext db, FaqService svc) Setup(long tenantId = 1)
    {
        var db = TestDb.New(tenantId);
        return (db, new FaqService(db));
    }

    private static SaveFaqRequest Req(string q = "Do you deliver to Coimbatore?", string a = "Yes, in 3-4 days.",
        string? category = "Delivery", int order = 1, bool published = true) => new(q, a, category, order, published);

    [Fact]
    public async Task A_created_faq_is_returned_to_the_storefront()
    {
        var (db, svc) = Setup();
        using var _ = db;

        var created = await svc.CreateAsync(Req());

        Assert.True(created.FaqId > 0);
        var published = await svc.PublishedAsync();
        Assert.Equal("Do you deliver to Coimbatore?", Assert.Single(published).Question);
    }

    [Fact]
    public async Task Unpublished_entries_are_hidden_from_the_storefront_but_visible_to_the_merchant()
    {
        var (db, svc) = Setup();
        using var _ = db;
        await svc.CreateAsync(Req());
        await svc.CreateAsync(Req(q: "Internal draft", a: "Not ready", published: false, order: 2));

        Assert.Single(await svc.PublishedAsync());
        Assert.Equal(2, (await svc.AllAsync()).Count);
    }

    [Fact]
    public async Task Entries_come_back_in_display_order()
    {
        var (db, svc) = Setup();
        using var _ = db;
        await svc.CreateAsync(Req(q: "Third", order: 30));
        await svc.CreateAsync(Req(q: "First", order: 10));
        await svc.CreateAsync(Req(q: "Second", order: 20));

        var ordered = (await svc.PublishedAsync()).Select(f => f.Question).ToList();
        Assert.Equal(new[] { "First", "Second", "Third" }, ordered);
    }

    [Theory]
    [InlineData("", "an answer")]
    [InlineData("a question", "")]
    [InlineData("   ", "   ")]
    public async Task A_question_and_answer_are_both_required(string q, string a)
    {
        var (db, svc) = Setup();
        using var _ = db;

        await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(new SaveFaqRequest(q, a, null, 1, true)));
    }

    [Fact]
    public async Task Updating_changes_content_and_publication()
    {
        var (db, svc) = Setup();
        using var _ = db;
        var created = await svc.CreateAsync(Req());

        var updated = await svc.UpdateAsync(created.FaqId,
            new SaveFaqRequest("Do you deliver to Erode?", "Yes, next day.", "Delivery", 5, false));

        Assert.Equal("Do you deliver to Erode?", updated.Question);
        Assert.False(updated.IsPublished);
        Assert.Empty(await svc.PublishedAsync());
        Assert.NotNull((await db.Faqs.SingleAsync()).UpdatedAt);
    }

    [Fact]
    public async Task Deleting_removes_it()
    {
        var (db, svc) = Setup();
        using var _ = db;
        var created = await svc.CreateAsync(Req());

        await svc.DeleteAsync(created.FaqId);

        Assert.Empty(await svc.AllAsync());
        await Assert.ThrowsAsync<AppException>(() => svc.DeleteAsync(created.FaqId));
    }

    [Fact]
    public async Task One_stores_faqs_are_invisible_to_another()
    {
        var dbName = Guid.NewGuid().ToString();
        using var tenant1 = TestDb.ForDatabase(dbName, 1);
        using var tenant2 = TestDb.ForDatabase(dbName, 2);

        await new FaqService(tenant1).CreateAsync(Req(q: "Tenant 1 only"));

        Assert.Single(await new FaqService(tenant1).AllAsync());
        Assert.Empty(await new FaqService(tenant2).AllAsync());
    }
}
