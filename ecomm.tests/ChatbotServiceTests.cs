using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using ecomm.api.Features.Faqs;
using ecomm.api.Features.Notifications;
using ecomm.api.Features.Orders;
using ecomm.api.Features.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// The chatbot orchestration core (v4 Phase 2): classify -> fetch real data -> compose a grounded
/// answer, with escalation on frustration/explicit-request/judgment-call/no-grounded-data/exchange
/// limit. Like SupportDraftTests, the model itself isn't under test — what matters is what it was
/// given to ground on, and that escalation triggers fire exactly when they should.
/// </summary>
public class ChatbotServiceTests
{
    private static (EcommerceDbContext db, ChatbotService svc, ScriptedAi ai, ContactTestFeed feed, RecordingRealtime realtime, FakeOrderService orders, FakeProductService products) Setup()
    {
        var db = TestDb.New(tenantId: 1);
        var ai = new ScriptedAi();
        var feed = new ContactTestFeed();
        var realtime = new RecordingRealtime();
        var conversations = new ShopperConversationService(
            db, DataProtectionProvider.Create("ecomm-tests"), feed, new RecordingEmail(),
            new FixedTenant(1), Options.Create(new TenancyOptions { BaseDomain = "wavcommerce.online", DefaultTenantId = 1 }),
            realtime);
        var orders = new FakeOrderService();
        var products = new FakeProductService();
        var svc = new ChatbotService(db, conversations, new PassThroughCredits(ai), new FaqService(db),
            orders, products, realtime, feed, new HelpdeskSettingsService(db), NullLogger<ChatbotService>.Instance);
        return (db, svc, ai, feed, realtime, orders, products);
    }

    private static long SeedConversation(EcommerceDbContext db, long shopperUserId = 5)
    {
        var convo = new SupportTicket
        {
            Axis = ConversationAxis.ShopperMerchant, Subject = "Question", Status = "Open",
            ShopperUserId = shopperUserId, ShopperEmail = "priya@example.com", CreatedAt = DateTime.UtcNow,
        };
        db.SupportTickets.Add(convo);
        db.SaveChanges();
        return convo.SupportTicketId;
    }

    private const string NotFrustrated = """{"frustrated":false,"wantsHuman":false,"topic":"general"}""";
    private const string OrderTopic = """{"frustrated":false,"wantsHuman":false,"topic":"order"}""";
    private const string Frustrated = """{"frustrated":true,"wantsHuman":false,"topic":"general"}""";
    private const string WantsHuman = """{"frustrated":false,"wantsHuman":true,"topic":"general"}""";
    private const string Grounded = """{"answer":"Yes, we ship worldwide.","grounded":true}""";
    private const string Ungrounded = """{"answer":"","grounded":false}""";

    [Fact]
    public async Task A_grounded_faq_question_gets_answered_and_not_escalated()
    {
        var (db, svc, ai, _, realtime, _, _) = Setup();
        using var _db = db;
        db.Faqs.Add(new Faq { TenantId = 1, Question = "Do you ship worldwide?", Answer = "Yes.", IsPublished = true, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        ai.Enqueue(NotFrustrated, Grounded);
        var id = SeedConversation(db);

        var result = await svc.HandleShopperMessageAsync(id, "Do you ship worldwide?", 5);

        Assert.False(result.Escalated);
        Assert.Equal("Yes, we ship worldwide.", result.Reply);
        Assert.Contains("Do you ship worldwide?", ai.Prompts[1].User);   // FAQ made it into the compose context
        Assert.Contains(realtime.Pushed, m => m.AuthorType == MessageAuthorType.Bot && m.Body == "Yes, we ship worldwide.");
        Assert.Equal(1, db.ChatbotConversationStates.Single().UnresolvedExchangeCount);
    }

    [Fact]
    public async Task An_order_question_grounds_on_the_customers_real_order_not_a_fabricated_one()
    {
        var (db, svc, ai, _, _, orders, _) = Setup();
        using var _db = db;
        ai.Enqueue(OrderTopic, """{"answer":"Your order ORD-1 has shipped.","grounded":true}""");
        orders.Mine = [new OrderListItem(70, "ORD-1", "Shipped", 999m, 1, "Widget", null, DateTime.UtcNow, DateTime.UtcNow, false)];
        orders.Detail = new OrderDto(70, "ORD-1", "Shipped", "INR", 999, 0, 0, 0, 999, DateTime.UtcNow, DateTime.UtcNow,
            [], null, null, null, null, null, null, false,
            new ShipmentDto(1, "Delhivery", "AWB1", "InTransit", DateTime.UtcNow.AddDays(2), null, null), []);
        var id = SeedConversation(db);

        var result = await svc.HandleShopperMessageAsync(id, "Where is order ORD-1?", 5);

        Assert.False(result.Escalated);
        Assert.Contains("ORD-1", ai.Prompts[1].User);
        Assert.Contains("Shipped", ai.Prompts[1].User);
        Assert.Contains("Delhivery", ai.Prompts[1].User);
        Assert.Contains("estimate, not a promise", ai.Prompts[1].User);
    }

    [Fact]
    public async Task Ungrounded_answer_escalates_and_logs_the_unanswered_question()
    {
        var (db, svc, ai, feed, _, _, _) = Setup();
        using var _db = db;
        ai.Enqueue(NotFrustrated, Ungrounded);
        var id = SeedConversation(db);

        var result = await svc.HandleShopperMessageAsync(id, "Do you support quantum teleportation delivery?", 5);

        Assert.True(result.Escalated);
        Assert.Equal("NoGroundedData", result.EscalationReason);
        Assert.Contains("connecting you with a team member", result.Reply);
        Assert.Equal("Do you support quantum teleportation delivery?", db.ChatbotUnansweredQuestions.Single().Question);
        Assert.False(db.ChatbotConversationStates.Single().IsBotActive);
        // One notification from persisting the shopper's own message (existing, tested behavior),
        // one more specifically flagging the escalation for the merchant to notice.
        Assert.Equal(2, feed.AdminNotifications.Count);
        Assert.Contains(feed.AdminNotifications, n => n.Title.Contains("escalated"));
    }

    [Fact]
    public async Task Frustration_escalates_without_ever_calling_compose()
    {
        var (db, svc, ai, _, _, _, _) = Setup();
        using var _db = db;
        ai.Enqueue(Frustrated);
        var id = SeedConversation(db);

        var result = await svc.HandleShopperMessageAsync(id, "This is ridiculous, still nothing!", 5);

        Assert.True(result.Escalated);
        Assert.Equal("Frustration", result.EscalationReason);
        Assert.Single(ai.Prompts);   // classify only — compose never runs once frustration is detected
    }

    [Fact]
    public async Task Explicit_human_request_escalates()
    {
        var (db, svc, ai, _, _, _, _) = Setup();
        using var _db = db;
        ai.Enqueue(WantsHuman);
        var id = SeedConversation(db);

        var result = await svc.HandleShopperMessageAsync(id, "Can I talk to a real person please", 5);

        Assert.True(result.Escalated);
        Assert.Equal("ExplicitRequest", result.EscalationReason);
    }

    [Theory]
    [InlineData("I want a refund")]
    [InlineData("can you give me a discount")]
    [InlineData("I'd like to cancel my order")]
    public async Task Judgment_call_language_escalates_by_rule_with_zero_AI_calls(string message)
    {
        var (db, svc, ai, _, _, _, _) = Setup();
        using var _db = db;
        var id = SeedConversation(db);

        var result = await svc.HandleShopperMessageAsync(id, message, 5);

        Assert.True(result.Escalated);
        Assert.Equal("JudgmentCall", result.EscalationReason);
        Assert.Empty(ai.Prompts);   // rule-based — the model is never even asked
    }

    [Fact]
    public async Task Once_escalated_the_bot_stays_silent_on_further_messages()
    {
        var (db, svc, ai, _, _, _, _) = Setup();
        using var _db = db;
        var id = SeedConversation(db);
        await svc.HandleShopperMessageAsync(id, "I want a refund", 5);   // escalates via judgment call
        ai.Enqueue(NotFrustrated, Grounded);   // even if this were queued, it must never be consumed

        var result = await svc.HandleShopperMessageAsync(id, "hello?", 5);

        Assert.True(result.Escalated);
        Assert.Equal("JudgmentCall", result.EscalationReason);   // reason is unchanged, not re-evaluated
        Assert.Empty(ai.Prompts);   // still never called
    }

    [Fact]
    public async Task Three_unresolved_exchanges_escalate_on_the_next_message()
    {
        var (db, svc, ai, _, _, _, _) = Setup();
        using var _db = db;
        var id = SeedConversation(db);
        for (var i = 0; i < 3; i++)
        {
            ai.Enqueue(NotFrustrated, Grounded);
            var r = await svc.HandleShopperMessageAsync(id, $"question {i}", 5);
            Assert.False(r.Escalated);
        }

        var result = await svc.HandleShopperMessageAsync(id, "one more question", 5);

        Assert.True(result.Escalated);
        Assert.Equal("ExchangeLimit", result.EscalationReason);
    }

    [Fact]
    public async Task A_classify_failure_degrades_gracefully_and_still_attempts_an_answer()
    {
        var (db, svc, ai, _, _, _, _) = Setup();
        using var _db = db;
        ai.ThrowOnNextCall = true;
        ai.Enqueue(Grounded);   // compose still runs with default (non-frustrated) classification
        var id = SeedConversation(db);

        var result = await svc.HandleShopperMessageAsync(id, "hi", 5);

        Assert.False(result.Escalated);
        Assert.Equal("Yes, we ship worldwide.", result.Reply);
    }

    [Fact]
    public async Task Disabling_the_chatbot_leaves_every_thread_to_a_human_with_zero_AI_calls()
    {
        var (db, svc, ai, _, _, _, _) = Setup();
        using var _db = db;
        db.Settings.Add(new ecomm.api.Data.Entities.Setting { TenantId = 1, SettingKey = "ChatbotEnabled", SettingValue = "false", DataType = "string", Category = "Helpdesk", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        var id = SeedConversation(db);

        var result = await svc.HandleShopperMessageAsync(id, "hello?", 5);

        Assert.True(result.Escalated);
        Assert.Empty(ai.Prompts);
        Assert.Empty(db.ChatbotConversationStates);   // never even created — the bot never engaged at all
    }

    [Fact]
    public async Task Escalation_outside_configured_active_hours_uses_the_delayed_response_message()
    {
        var (db, svc, ai, _, _, _, _) = Setup();
        using var _db = db;
        // A one-minute window that can't possibly contain "now", regardless of when the test runs.
        var almostNow = TimeOnly.FromDateTime(DateTime.UtcNow).AddMinutes(-2).ToString("HH:mm");
        var justBefore = TimeOnly.FromDateTime(DateTime.UtcNow).AddMinutes(-1).ToString("HH:mm");
        db.Settings.Add(new ecomm.api.Data.Entities.Setting { TenantId = 1, SettingKey = "ChatbotActiveHoursStart", SettingValue = almostNow, DataType = "string", Category = "Helpdesk", CreatedAt = DateTime.UtcNow });
        db.Settings.Add(new ecomm.api.Data.Entities.Setting { TenantId = 1, SettingKey = "ChatbotActiveHoursEnd", SettingValue = justBefore, DataType = "string", Category = "Helpdesk", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        var id = SeedConversation(db);

        var result = await svc.HandleShopperMessageAsync(id, "I want a refund", 5);

        Assert.True(result.Escalated);
        Assert.Contains("support hours resume", result.Reply);
    }

    [Fact]
    public async Task Starting_a_new_chat_creates_the_ticket_and_answers_the_first_message_without_double_persisting()
    {
        var (db, svc, ai, _, _, _, _) = Setup();
        using var _db = db;
        db.Faqs.Add(new Faq { TenantId = 1, Question = "Do you ship worldwide?", Answer = "Yes.", IsPublished = true, CreatedAt = DateTime.UtcNow });
        db.Users.Add(new User { UserId = 5, TenantId = 1, Email = "priya@example.com", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        ai.Enqueue(NotFrustrated, Grounded);

        var (conversationId, reply) = await svc.StartShopperChatAsync("Do you ship worldwide?", 5);

        Assert.False(reply.Escalated);
        Assert.Equal(ConversationAxis.ShopperMerchant, db.SupportTickets.Single(t => t.SupportTicketId == conversationId).Axis);
        // Exactly one shopper message and one bot message — no duplicate from the create step.
        var messages = db.SupportMessages.Where(m => m.SupportTicketId == conversationId).ToList();
        Assert.Single(messages, m => m.AuthorType == MessageAuthorType.Shopper);
        Assert.Single(messages, m => m.AuthorType == MessageAuthorType.Bot);
    }

    [Fact]
    public async Task Starting_a_chat_without_an_account_email_is_rejected()
    {
        var (db, svc, _, _, _, _, _) = Setup();
        using var _db = db;
        db.Users.Add(new User { UserId = 5, TenantId = 1, Email = null, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        await Assert.ThrowsAsync<AppException>(() => svc.StartShopperChatAsync("hi", 5));
    }

    [Fact]
    public async Task A_conversation_the_caller_does_not_own_is_not_found()
    {
        var (db, svc, ai, _, _, _, _) = Setup();
        using var _db = db;
        ai.Enqueue(NotFrustrated, Grounded);
        var id = SeedConversation(db, shopperUserId: 5);

        await Assert.ThrowsAsync<AppException>(() => svc.HandleShopperMessageAsync(id, "hi", 999));
    }

    private sealed class ScriptedAi : IAiService
    {
        private readonly Queue<string> _responses = new();
        public List<AiPrompt> Prompts { get; } = new();
        public bool ThrowOnNextCall;
        public bool Enabled => true;
        public long EstimateCostMicros(AiCompletion completion) => 0;
        public void Enqueue(params string[] responses) { foreach (var r in responses) _responses.Enqueue(r); }

        public Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
        {
            if (ThrowOnNextCall) { ThrowOnNextCall = false; throw new InvalidOperationException("simulated classify failure"); }
            Prompts.Add(prompt);
            var text = _responses.Count > 0 ? _responses.Dequeue() : """{"answer":"","grounded":false}""";
            return Task.FromResult(new AiCompletion(text, 10, 10, "test-model"));
        }
    }

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

    private sealed class FakeOrderService : IOrderService
    {
        public List<OrderListItem> Mine { get; set; } = [];
        public OrderDto? Detail { get; set; }
        public Task<List<OrderListItem>> ListMineAsync(long userId, CancellationToken ct = default) => Task.FromResult(Mine);
        public Task<OrderDto?> GetAsync(long orderId, long? userId, bool isAdmin, CancellationToken ct = default) => Task.FromResult(Detail);
        public Task<CheckoutQuoteDto> QuoteAsync(long userId, long? shippingAddressId, string? couponCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PlaceOrderResult> PlaceOrderAsync(long userId, PlaceOrderRequest req, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OrderDto> ConfirmPaymentAsync(long userId, long orderId, ConfirmPaymentRequest req, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OrderDto> CancelOrderAsync(long userId, long orderId, CancelOrderRequest req, bool isAdmin, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PagedResult<OrderListItem>> ListAllAsync(string? status, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OrderDto?> UpdateStatusAsync(long orderId, string toStatus, long? userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OrderDto?> CreateShipmentAsync(long orderId, CreateShipmentRequest req, long? userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OrderDto?> ShipWithShiprocketAsync(long orderId, long? userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OrderDto?> SchedulePickupAsync(long orderId, long? userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GenerateShiprocketLabelAsync(long orderId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OrderDto?> ReshipAsync(long orderId, long? userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OrderDto?> MarkDeliveredAsync(long orderId, long? userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeProductService : IProductService
    {
        public PagedResult<ProductListItemDto> Results { get; set; } = new() { Items = [], Page = 1, PageSize = 5, TotalCount = 0 };
        public Task<PagedResult<ProductListItemDto>> BrowseAsync(ProductQuery query, bool adminView, CancellationToken ct = default) => Task.FromResult(Results);
        public Task<FacetsDto> FacetsAsync(ProductQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProductDetailDto?> GetByIdAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProductDetailDto> CreateAsync(SaveProductRequest req, long? userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProductDetailDto?> UpdateAsync(long id, SaveProductRequest req, long? userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<ProductListItemDto>> GetFrequentlyBoughtTogetherAsync(long productId, int take, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
