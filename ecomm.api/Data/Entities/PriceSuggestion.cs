namespace ecomm.api.Data.Entities;

/// <summary>One pricing engine suggestion — the full audit trail (per product and platform-wide)
/// the plan calls for, since every row here persists regardless of outcome. The engine computes
/// <see cref="SuggestedPrice"/> deterministically from the three signal percentages; the model's
/// only job (v4 Phase 5) is phrasing <see cref="Reason"/> in plain language from those same numbers
/// — it never computes the price itself.</summary>
public class PriceSuggestion : ITenantScoped
{
    public long PriceSuggestionId { get; set; }
    public long TenantId { get; set; } = 1;
    public long ProductId { get; set; }
    public decimal OldPrice { get; set; }
    public decimal SuggestedPrice { get; set; }
    /// <summary>Stock level vs. reorder level — the one signal with real data from day one.</summary>
    public decimal InventorySignalPercent { get; set; }
    /// <summary>View/purchase velocity — always 0 until Phase 3 Track B's event capture ships;
    /// deliberately neutral, never fabricated.</summary>
    public decimal DemandSignalPercent { get; set; }
    /// <summary>Active <see cref="PricingSeasonRule"/> bias for this product's category, if any.</summary>
    public decimal SeasonalitySignalPercent { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";   // Pending | Approved | Rejected
    public DateTime SuggestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public long? ApprovedByUserId { get; set; }
    public DateTime? AppliedAt { get; set; }

    public Product? Product { get; set; }
}
