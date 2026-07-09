namespace ecomm.api.Data.Entities;

/// <summary>
/// A merchant's own payment configuration. Each tenant sets their provider + keys so
/// payments settle to their account. <see cref="RazorpayKeySecret"/> is stored as
/// Data-Protection ciphertext (never plaintext, never returned to the client).
/// </summary>
public class TenantPaymentAccount : ITenantScoped
{
    public long TenantPaymentAccountId { get; set; }
    public long TenantId { get; set; }
    public string Provider { get; set; } = "Mock";   // Mock | Razorpay
    public string? RazorpayKeyId { get; set; }
    public string? RazorpayKeySecret { get; set; }    // encrypted at rest
    public string? AccountId { get; set; }
    public bool IsVerified { get; set; }
    public bool IsEnabled { get; set; }               // merchant flipped payments live
    public DateTime? ConnectedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
