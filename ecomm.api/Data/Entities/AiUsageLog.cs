namespace ecomm.api.Data.Entities;

/// <summary>
/// Signed ledger of every AI credit movement for a tenant. <c>Credits</c> &lt; 0 = spent on an AI action;
/// &gt; 0 = granted (plan cycle) or bought (top-up). Tokens/CostMicros/Model are populated for real AI
/// calls only (<c>CostMicros</c> = the platform's real provider cost in ₹ ×1e6, for margin tuning).
/// </summary>
public class AiUsageLog : ITenantScoped
{
    public long AiUsageLogId { get; set; }
    public long TenantId { get; set; }
    public string Feature { get; set; } = string.Empty;   // improve-text | seo | page | sample-catalog | topup | grant
    public int Credits { get; set; }                      // signed: negative = debit, positive = credit
    public int? Tokens { get; set; }                      // total tokens (AI calls only)
    public long? CostMicros { get; set; }                 // platform provider cost, ₹ ×1_000_000
    public string? Model { get; set; }
    public long? UserId { get; set; }                     // acting admin/staff user
    public DateTime CreatedAt { get; set; }
}
