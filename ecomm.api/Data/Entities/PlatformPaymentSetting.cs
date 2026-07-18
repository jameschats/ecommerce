namespace ecomm.api.Data.Entities;

/// <summary>
/// The PLATFORM's own payment gateway config (merchants paying us: subscriptions, AI credits).
/// Single row, platform-level (NOT tenant-scoped). When present it overrides the app-wide
/// <c>Payments</c> env config, so keys can be rotated from the console instead of api.env.
/// </summary>
public class PlatformPaymentSetting
{
    public byte PlatformPaymentSettingId { get; set; } = 1;
    public string Provider { get; set; } = "Mock";   // Mock | Razorpay
    public string? RazorpayKeyId { get; set; }
    public string? RazorpayKeySecret { get; set; }   // encrypted at rest
    public DateTime? UpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
