namespace ecomm.api.Data.Entities;

/// <summary>
/// A merchant's own Shiprocket connection (per-tenant, mirroring <see cref="TenantPaymentAccount"/>).
/// The API password is stored as Data-Protection ciphertext (never plaintext, never returned to the
/// client). The bearer token is short-lived (~10 days) and cached in-memory per tenant, not stored here.
/// </summary>
public class TenantShippingAccount : ITenantScoped
{
    public long TenantShippingAccountId { get; set; }
    public long TenantId { get; set; }
    public string Provider { get; set; } = "Shiprocket";   // Shiprocket (only provider for now)
    public string? Email { get; set; }                      // Shiprocket API-user email
    public string? PasswordCipher { get; set; }             // encrypted at rest
    public string? PickupPincode { get; set; }              // origin pincode for rate lookups
    public string? PickupLocation { get; set; }             // registered Shiprocket pickup-location nickname
    public bool IsVerified { get; set; }                    // last auth succeeded
    public bool IsEnabled { get; set; }                     // merchant flipped Shiprocket live
    public DateTime? ConnectedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
