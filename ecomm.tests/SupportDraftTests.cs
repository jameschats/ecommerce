using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Support;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// AI draft replies (C3). The model itself isn't under test — what matters is the CONTEXT it's
/// handed, because a draft can only be as truthful as the facts it was given. These capture the
/// prompt and assert what's in it, and what must never be.
/// </summary>
public class SupportDraftTests
{
    private static (EcommerceDbContext db, SupportDraftService svc, CapturingAi ai) Setup()
    {
        var db = TestDb.New(tenantId: 1);
        var ai = new CapturingAi();
        return (db, new SupportDraftService(db, new PassThroughCredits(ai)), ai);
    }

    private static long SeedConversation(EcommerceDbContext db, long? orderId = null)
    {
        var convo = new SupportTicket
        {
            Axis = ConversationAxis.ShopperMerchant, Subject = "Where is my order?",
            Status = "Open", ShopperEmail = "priya@example.com", OrderId = orderId,
            CreatedAt = DateTime.UtcNow,
        };
        db.SupportTickets.Add(convo);
        db.SaveChanges();

        db.SupportMessages.Add(new SupportMessage
        {
            SupportTicketId = convo.SupportTicketId, AuthorType = MessageAuthorType.Shopper,
            Body = "It's been a week and nothing has arrived.", CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return convo.SupportTicketId;
    }

    [Fact]
    public async Task The_prompt_carries_the_thread_and_reports_what_it_used()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;
        var id = SeedConversation(db);

        var result = await svc.DraftReplyAsync(id);

        Assert.Contains("It's been a week and nothing has arrived.", ai.LastPrompt!.User);
        Assert.Contains("Customer:", ai.LastPrompt.User);
        Assert.Contains(result.GroundedOn, g => g.Contains("message"));
    }

    [Fact]
    public async Task A_linked_order_contributes_real_status_and_courier_scans()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;
        db.Orders.Add(new Order
        {
            OrderId = 70, TenantId = 1, UserId = 5, OrderNumber = "ORD20260722-00070",
            Status = "Shipped", PlacedAt = DateTime.UtcNow.AddDays(-7), CreatedAt = DateTime.UtcNow.AddDays(-7),
        });
        db.Shipments.Add(new Shipment
        {
            ShipmentId = 12, TenantId = 1, OrderId = 70, Provider = "Shiprocket",
            Courier = "Delhivery", TrackingNumber = "AWB777", Status = "InTransit", CreatedAt = DateTime.UtcNow,
        });
        db.ShipmentCheckpoints.Add(new ShipmentCheckpoint
        {
            ShipmentId = 12, TenantId = 1, RawStatus = "Out for delivery",
            Location = "Chennai", CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        var id = SeedConversation(db, orderId: 70);

        var result = await svc.DraftReplyAsync(id);

        Assert.Contains("ORD20260722-00070", ai.LastPrompt!.User);
        Assert.Contains("Shipped", ai.LastPrompt.User);
        Assert.Contains("AWB777", ai.LastPrompt.User);
        Assert.Contains("Out for delivery", ai.LastPrompt.User);
        Assert.Contains("Chennai", ai.LastPrompt.User);
        Assert.Contains(result.GroundedOn, g => g.Contains("courier scan"));
    }

    [Fact]
    public async Task Published_faqs_are_included_and_unpublished_ones_are_not()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;
        db.Faqs.Add(new Faq { TenantId = 1, Question = "Do you ship to Erode?", Answer = "Yes, in 2 days.", IsPublished = true, CreatedAt = DateTime.UtcNow });
        db.Faqs.Add(new Faq { TenantId = 1, Question = "Internal draft", Answer = "SECRET-MARGIN-NOTE", IsPublished = false, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        var id = SeedConversation(db);

        await svc.DraftReplyAsync(id);

        Assert.Contains("Do you ship to Erode?", ai.LastPrompt!.User);
        Assert.DoesNotContain("SECRET-MARGIN-NOTE", ai.LastPrompt.User);
    }

    [Fact]
    public async Task Internal_notes_never_reach_the_model()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;
        var id = SeedConversation(db);
        db.SupportMessages.Add(new SupportMessage
        {
            SupportTicketId = id, AuthorType = MessageAuthorType.Merchant, IsInternalNote = true,
            Body = "known repeat complainer, do not refund", CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        await svc.DraftReplyAsync(id);

        Assert.DoesNotContain("repeat complainer", ai.LastPrompt!.User);
    }

    [Fact]
    public async Task Policy_html_is_flattened_to_prose()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;
        db.StorePolicies.Add(new StorePolicy
        {
            TenantId = 1, Handle = "refund", Title = "Refunds",
            BodyHtml = "<p>Returns accepted within <b>7&nbsp;days</b>.</p>", CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        var id = SeedConversation(db);

        await svc.DraftReplyAsync(id);

        Assert.Contains("Returns accepted within 7 days.", ai.LastPrompt!.User);
        Assert.DoesNotContain("<p>", ai.LastPrompt.User);
    }

    [Fact]
    public async Task The_system_prompt_forbids_inventing_facts_and_making_commitments()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;
        var id = SeedConversation(db);

        await svc.DraftReplyAsync(id);

        var system = ai.LastPrompt!.System;
        Assert.Contains("ONLY the facts", system);
        Assert.Contains("Never promise a delivery date", system);
        Assert.Contains("Never offer a refund", system);
    }

    [Fact]
    public async Task A_merchant_platform_ticket_is_not_draftable_here()
    {
        var (db, svc, _) = Setup();
        using var _db = db;
        db.SupportTickets.Add(new SupportTicket
        {
            Axis = ConversationAxis.MerchantPlatform, Subject = "Payouts", Status = "Open", CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        var platformTicketId = db.SupportTickets.First().SupportTicketId;

        await Assert.ThrowsAsync<AppException>(() => svc.DraftReplyAsync(platformTicketId));
    }

    /// <summary>Captures the prompt instead of calling a provider.</summary>
    private sealed class CapturingAi : IAiService
    {
        public AiPrompt? LastPrompt { get; private set; }
        public bool Enabled => true;
        public long EstimateCostMicros(AiCompletion completion) => 0;

        public Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
        {
            LastPrompt = prompt;
            return Task.FromResult(new AiCompletion("Thanks for checking in — your order is on its way.", 100, 20, "test-model"));
        }
    }

    /// <summary>Runs the metered action without touching balances; metering itself is covered by AiCreditTests.</summary>
    private sealed class PassThroughCredits(IAiService ai) : IAiCreditService
    {
        public async Task<T> MeterAsync<T>(string feature, Func<IAiService, Task<(T, AiCompletion)>> action, CancellationToken ct = default)
        {
            var (result, _) = await action(ai);
            return result;
        }

        public Task<T> MeterImageAsync<T>(string feature, Func<IImageAiService, Task<(T, ImageResult)>> action, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AiBalanceDto> GetBalanceAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiUsageDto>> GetUsageAsync(int take = 50, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AiCreditPack?> GetPackAsync(int packId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> TopUpAsync(int packId, string reference, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
