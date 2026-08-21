namespace ecomm.api.Data.Entities;

/// <summary>Per-conversation chatbot state — whether the bot is still handling this thread or has
/// handed off to a human, and how many exchanges it's tried. One row per <see cref="SupportTicket"/>
/// on the ShopperMerchant axis; created lazily on the conversation's first bot-handled message.</summary>
public class ChatbotConversationState : ITenantScoped
{
    public long ChatbotConversationStateId { get; set; }
    public long TenantId { get; set; } = 1;
    public long SupportTicketId { get; set; }
    public bool IsBotActive { get; set; } = true;
    public int UnresolvedExchangeCount { get; set; }
    public DateTime? EscalatedAt { get; set; }
    /// <summary>NoGroundedData | Frustration | ExplicitRequest | JudgmentCall | ExchangeLimit — null while still bot-active.</summary>
    public string? EscalationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A question the bot couldn't answer from FAQ/catalog/order context — the merchant-facing
/// content-gap feedback loop the plan doc calls for, feeding back into what FAQs are worth adding.</summary>
public class ChatbotUnansweredQuestion : ITenantScoped
{
    public long ChatbotUnansweredQuestionId { get; set; }
    public long TenantId { get; set; } = 1;
    public long? SupportTicketId { get; set; }
    public string Question { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
