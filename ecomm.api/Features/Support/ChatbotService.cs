using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using ecomm.api.Features.Faqs;
using ecomm.api.Features.Notifications;
using ecomm.api.Features.Orders;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Support;

public sealed record ChatbotReplyDto(string Reply, bool Escalated, string? EscalationReason);
public sealed record StartChatResponse(long ConversationId, ChatbotReplyDto Reply);

public interface IChatbotService
{
    /// <summary>Handles one customer chat turn: persists the customer's message, decides whether
    /// the bot can answer or must hand off to a human, and (if answering) persists + returns the
    /// bot's reply. Requires an authenticated shopper — anonymous livechat is a known v1 gap (the
    /// bot needs "which of this customer's orders" lookups the same way SupportDraftService needs
    /// a linked order; there's no equivalent identity for an anonymous token-based thread yet).</summary>
    Task<ChatbotReplyDto> HandleShopperMessageAsync(long conversationId, string message, long shopperUserId, CancellationToken ct = default);

    /// <summary>Starts a brand-new conversation for the widget's first message. Deliberately not
    /// <c>IShopperConversationService.StartAsync</c> + <c>HandleShopperMessageAsync</c> in sequence —
    /// that would persist the first message twice (once via StartAsync's own write, once via the
    /// chatbot's). Creates the ticket directly, then hands off to the same message-handling path.</summary>
    Task<(long ConversationId, ChatbotReplyDto Reply)> StartShopperChatAsync(string message, long shopperUserId, CancellationToken ct = default);
}

/// <summary>
/// The chatbot's orchestration core (v4 Phase 2): classify → fetch real data → compose a grounded
/// answer, extending <c>SupportDraftService</c>'s exact discipline (facts-only, never invent
/// status/dates/stock, never promise a refund/discount/exception) for live, multi-turn customer
/// conversation instead of one-shot merchant-reply drafting. A conversation IS a
/// <see cref="ConversationAxis.ShopperMerchant"/> ticket from message one — escalation doesn't
/// convert anything, it just stops the bot from replying and lets a human pick up the same thread.
/// </summary>
public sealed class ChatbotService(
    EcommerceDbContext db,
    IShopperConversationService conversations,
    IAiCreditService credits,
    IFaqService faqs,
    IOrderService orders,
    IProductService products,
    IConversationRealtime realtime,
    INotificationFeedService feed,
    IHelpdeskSettingsService helpdeskSettings,
    ILogger<ChatbotService> logger) : IChatbotService
{
    private const int MaxUnresolvedExchanges = 3;
    private const string HandoffMessage = "I'm connecting you with a team member who can help further — they'll pick up right here in this chat.";
    private const string HandoffMessageOutsideHours = "I'm connecting you with a team member who can help further — they'll pick up right here in this chat, though it may not be until our support hours resume.";

    // Judgment calls escalate unconditionally, by rule rather than model confidence — refund/
    // exception decisions are the merchant's to make, never the bot's, regardless of how well it
    // could technically answer.
    private static readonly Regex JudgmentCallPattern = new(
        @"\b(refunds?|returns?\s+(this|my|it)|money\s*back|discounts?|compensat\w*|goodwill|exceptions?|cancel\s+(my|this)\s+order|charge\s*back|dispute)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string ClassifySystemPrompt =
        "You classify one customer support chat message for an e-commerce store. " +
        "Respond with ONLY strict JSON, no other text: " +
        "{\"frustrated\": boolean, \"wantsHuman\": boolean, \"topic\": \"order\"|\"product\"|\"general\"}. " +
        "frustrated=true only for clear anger or frustration, not routine questions. " +
        "wantsHuman=true only if they explicitly ask to speak to a human, agent, or real person. " +
        "topic=\"order\" for order status/shipping/delivery questions, \"product\" for stock/price/product questions, otherwise \"general\".";

    private const string ComposeSystemPrompt =
        "You are a helpful customer support assistant for an online store, chatting directly with a customer. " +
        "Answer ONLY using facts in the CONTEXT below — never invent order status, dates, prices, or stock levels. " +
        "Never promise a delivery date; if an estimate is given, repeat it labeled as an estimate, not a promise. " +
        "Never offer a refund, discount, replacement, or any exception — say a team member will help with that. " +
        "If the CONTEXT doesn't actually answer the question, say so honestly rather than guessing. " +
        "Keep answers short and conversational, like a real chat message, not an email. " +
        "Respond with ONLY strict JSON, no other text: {\"answer\": \"your reply text\", \"grounded\": boolean}. " +
        "Set grounded=false if you could not really answer from the CONTEXT — this hands the conversation to a human.";

    public async Task<ChatbotReplyDto> HandleShopperMessageAsync(long conversationId, string message, long shopperUserId, CancellationToken ct = default)
    {
        message = Trim(message);
        if (message.Length == 0) throw new AppException("Message is empty.", StatusCodes.Status400BadRequest);

        var convo = await db.SupportTickets
            .Where(c => c.Axis == ConversationAxis.ShopperMerchant)
            .FirstOrDefaultAsync(c => c.SupportTicketId == conversationId, ct)
            ?? throw new AppException("Conversation not found.", StatusCodes.Status404NotFound);
        if (convo.ShopperUserId != shopperUserId)
            throw new AppException("Conversation not found.", StatusCodes.Status404NotFound);

        // Persist the customer's own message via the existing, tested path — same realtime push,
        // admin notification, and reopen-on-reply status handling as a message to a human agent.
        await conversations.ReplyAsShopperAsync(conversationId, message, shopperUserId, token: null, ct);

        var settings = await helpdeskSettings.GetAsync(ct);
        if (!settings.ChatbotEnabled)
            return new ChatbotReplyDto("", true, null);   // merchant turned the bot off entirely — every thread is human-owned from message one

        var state = await GetOrCreateStateAsync(conversationId, ct);

        if (!state.IsBotActive)
            return new ChatbotReplyDto("", true, state.EscalationReason);   // a human already owns this thread; bot stays silent

        if (JudgmentCallPattern.IsMatch(message))
            return await EscalateAsync(convo, state, "JudgmentCall", settings, ct);

        if (state.UnresolvedExchangeCount >= MaxUnresolvedExchanges)
            return await EscalateAsync(convo, state, "ExchangeLimit", settings, ct);

        var classification = await ClassifyAsync(message, ct);
        if (classification.Frustrated) return await EscalateAsync(convo, state, "Frustration", settings, ct);
        if (classification.WantsHuman) return await EscalateAsync(convo, state, "ExplicitRequest", settings, ct);

        var context = await BuildContextAsync(classification.Topic, message, shopperUserId, ct);
        var (answer, grounded) = await ComposeAsync(context, ct);

        if (!grounded)
        {
            db.ChatbotUnansweredQuestions.Add(new ChatbotUnansweredQuestion
            {
                SupportTicketId = conversationId, Question = message, CreatedAt = DateTime.UtcNow,
            });
            return await EscalateAsync(convo, state, "NoGroundedData", settings, ct);
        }

        state.UnresolvedExchangeCount++;
        state.UpdatedAt = DateTime.UtcNow;
        await AppendBotMessageAsync(convo, answer, ct);
        return new ChatbotReplyDto(answer, false, null);
    }

    public async Task<(long ConversationId, ChatbotReplyDto Reply)> StartShopperChatAsync(string message, long shopperUserId, CancellationToken ct = default)
    {
        var email = await db.Users.Where(u => u.UserId == shopperUserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(email))
            throw new AppException("We need a valid email address on your account to start a chat.", StatusCodes.Status400BadRequest);

        var now = DateTime.UtcNow;
        var convo = new SupportTicket
        {
            Axis = ConversationAxis.ShopperMerchant, Subject = "Live chat", Status = "Open", Priority = "Normal",
            ShopperUserId = shopperUserId, ShopperEmail = email, LastMessageAt = now, CreatedAt = now,
        };
        db.SupportTickets.Add(convo);
        await db.SaveChangesAsync(ct);
        convo.Reference = SupportService.BuildReference(now, convo.SupportTicketId);
        await db.SaveChangesAsync(ct);

        var reply = await HandleShopperMessageAsync(convo.SupportTicketId, message, shopperUserId, ct);
        return (convo.SupportTicketId, reply);
    }

    private async Task<ChatbotReplyDto> EscalateAsync(SupportTicket convo, ChatbotConversationState state, string reason, HelpdeskSettingsDto settings, CancellationToken ct)
    {
        state.IsBotActive = false;
        state.EscalatedAt = DateTime.UtcNow;
        state.EscalationReason = reason;
        state.UpdatedAt = DateTime.UtcNow;

        var closing = IsWithinActiveHours(settings) ? HandoffMessage : HandoffMessageOutsideHours;
        await AppendBotMessageAsync(convo, closing, ct);

        await feed.NotifyAdminsAsync("Conversation", $"Chatbot escalated on {convo.Reference ?? "a conversation"}",
            reason, $"/admin/inbox/{convo.SupportTicketId}", ct);

        return new ChatbotReplyDto(closing, true, reason);
    }

    /// <summary>True if no hours are configured (always active) or the current UTC time falls
    /// within the configured window — including an overnight window (e.g. 22:00-06:00). This
    /// doesn't change escalation triggers, only the messaging shown once escalation happens
    /// (per the resolved solo-seller design decision), and compares in UTC as a known v1
    /// simplification rather than the store's own configured Timezone.</summary>
    private static bool IsWithinActiveHours(HelpdeskSettingsDto settings)
    {
        if (settings.ActiveHoursStart is null || settings.ActiveHoursEnd is null) return true;
        if (!TimeOnly.TryParse(settings.ActiveHoursStart, out var start) || !TimeOnly.TryParse(settings.ActiveHoursEnd, out var end)) return true;

        var now = TimeOnly.FromDateTime(DateTime.UtcNow);
        return start <= end ? now >= start && now <= end : now >= start || now <= end;
    }

    private async Task AppendBotMessageAsync(SupportTicket convo, string body, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var msg = new SupportMessage
        {
            SupportTicketId = convo.SupportTicketId, AuthorUserId = null,
            AuthorType = MessageAuthorType.Bot, Body = body, CreatedAt = now,
        };
        db.SupportMessages.Add(msg);
        convo.LastMessageAt = now;
        convo.UpdatedAt = now;
        await db.SaveChangesAsync(ct);   // save before broadcast — same invariant every other reply path follows
        await realtime.MessageAsync(new LiveMessageDto(convo.SupportTicketId, msg.SupportMessageId, MessageAuthorType.Bot, body, now), ct);
    }

    private async Task<ChatbotConversationState> GetOrCreateStateAsync(long conversationId, CancellationToken ct)
    {
        var state = await db.ChatbotConversationStates.FirstOrDefaultAsync(s => s.SupportTicketId == conversationId, ct);
        if (state is not null) return state;

        state = new ChatbotConversationState
        {
            SupportTicketId = conversationId, IsBotActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.ChatbotConversationStates.Add(state);
        await db.SaveChangesAsync(ct);
        return state;
    }

    private sealed record Classification(bool Frustrated, bool WantsHuman, string Topic);

    private async Task<Classification> ClassifyAsync(string message, CancellationToken ct)
    {
        try
        {
            return await credits.MeterAsync(AiCreditPricing.ChatbotClassify, async ai =>
            {
                var completion = await ai.CompleteAsync(new AiPrompt(ClassifySystemPrompt, message, Json: true, MaxTokens: 100), ct);
                return (ParseClassification(completion.Text), completion);
            }, ct);
        }
        catch (Exception ex)
        {
            // A classify hiccup shouldn't break a live chat for the customer — degrade to "just try
            // to answer"; ComposeAsync's own credit check still surfaces a real out-of-credits error.
            logger.LogWarning(ex, "Chatbot classify failed — defaulting to unclassified.");
            return new Classification(false, false, "general");
        }
    }

    private static Classification ParseClassification(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new Classification(
                root.TryGetProperty("frustrated", out var f) && f.ValueKind == JsonValueKind.True,
                root.TryGetProperty("wantsHuman", out var w) && w.ValueKind == JsonValueKind.True,
                root.TryGetProperty("topic", out var t) ? t.GetString() ?? "general" : "general");
        }
        catch { return new Classification(false, false, "general"); }
    }

    private async Task<string> BuildContextAsync(string topic, string message, long shopperUserId, CancellationToken ct)
    {
        var sb = new StringBuilder();

        var faqList = await faqs.PublishedAsync(ct);
        if (faqList.Count > 0)
        {
            sb.AppendLine("FAQs:");
            foreach (var f in faqList.Take(20))
                sb.AppendLine($"- Q: {f.Question}\n  A: {f.Answer}");
        }

        if (topic == "order")
            await AppendOrderContextAsync(sb, message, shopperUserId, ct);

        if (topic == "product")
            await AppendProductContextAsync(sb, message, ct);

        sb.AppendLine();
        sb.AppendLine($"Customer's message: {message}");
        return sb.ToString();
    }

    private async Task AppendOrderContextAsync(StringBuilder sb, string message, long shopperUserId, CancellationToken ct)
    {
        var mine = await orders.ListMineAsync(shopperUserId, ct);
        var matched = mine.FirstOrDefault(o => message.Contains(o.orderNumber, StringComparison.OrdinalIgnoreCase));
        if (matched is not null)
        {
            var detail = await orders.GetAsync(matched.orderId, shopperUserId, isAdmin: false, ct);
            if (detail is not null) sb.AppendLine(DescribeOrder(detail));
            return;
        }
        if (mine.Count > 0)
        {
            sb.AppendLine("Customer's recent orders (ask which one if it's not clear which they mean):");
            foreach (var o in mine.Take(5))
                sb.AppendLine($"- {o.orderNumber}: {o.status}, placed {o.placedAt:yyyy-MM-dd}");
        }
    }

    private static string DescribeOrder(OrderDto o)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Order {o.orderNumber}: status {o.status}, placed {o.placedAt:yyyy-MM-dd}, total {o.currency} {o.totalAmount:0.00}.");
        if (o.shipment is { } s)
        {
            sb.AppendLine($"Shipment: courier {s.courier ?? "unassigned"}, tracking {s.trackingNumber ?? "not yet assigned"}, status {s.status}.");
            if (s.estimatedDeliveryDate is { } eta)
                sb.AppendLine($"Estimated delivery: {eta:yyyy-MM-dd} — this is an estimate, not a promise.");
        }
        return sb.ToString();
    }

    private async Task AppendProductContextAsync(StringBuilder sb, string message, CancellationToken ct)
    {
        var results = await products.BrowseAsync(new ProductQuery(Search: message, CategoryId: null, BrandId: null,
            Status: "Active", IsFeatured: null, Sort: null, Page: 1, PageSize: 5), adminView: false, ct);
        if (results.Items.Count == 0) return;

        sb.AppendLine("Matching products (live stock/price):");
        foreach (var p in results.Items)
            sb.AppendLine($"- {p.Name}: {(p.InStock ? $"in stock ({p.AvailableQty} available)" : "out of stock")}, price {p.Price:0.00}.");
    }

    private async Task<(string answer, bool grounded)> ComposeAsync(string context, CancellationToken ct) =>
        await credits.MeterAsync(AiCreditPricing.ChatbotReply, async ai =>
        {
            var completion = await ai.CompleteAsync(new AiPrompt(ComposeSystemPrompt, context, Json: true, MaxTokens: 300), ct);
            return (ParseAnswer(completion.Text), completion);
        }, ct);

    private static (string answer, bool grounded) ParseAnswer(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var answer = root.TryGetProperty("answer", out var a) ? a.GetString() ?? "" : "";
            var grounded = root.TryGetProperty("grounded", out var g) && g.ValueKind == JsonValueKind.True;
            return (answer, grounded && answer.Length > 0);
        }
        catch
        {
            // Malformed JSON from the model — treat as ungrounded rather than surfacing raw/garbage text to the customer.
            return ("", false);
        }
    }

    private static string Trim(string? v)
    {
        var t = (v ?? string.Empty).Trim();
        return t.Length <= 4000 ? t : t[..4000];
    }
}
