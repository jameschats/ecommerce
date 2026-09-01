namespace ecomm.api.Data.Entities;

/// <summary>
/// One merchant's OAuth connection to a social platform (Marketing Studio MS1). Standard SaaS model:
/// WavCommerce registers ONE app per network; each tenant connects their own account under it and we
/// store a per-tenant token here (see marketing-studio-plan.md §3.11). A row exists only once a tenant
/// has connected (or previously connected) a platform; "not connected" = no row. Tokens are encrypted
/// at rest via IDataProtection. Part of the Marketing Studio module — Marketing* table cluster, no FKs
/// into core commerce tables.
/// </summary>
public class SocialConnection : ITenantScoped
{
    public long SocialConnectionId { get; set; }
    public long TenantId { get; set; }

    /// <summary>Platform key: facebook | instagram | linkedin | pinterest | youtube | googleads | whatsapp.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Encrypted OAuth tokens (never stored in the clear).</summary>
    public string? AccessTokenCipher { get; set; }
    public string? RefreshTokenCipher { get; set; }

    /// <summary>The connected account/page/channel id and a human label to show on the card.</summary>
    public string? ExternalAccountId { get; set; }
    public string? AccountName { get; set; }
    public string? Scopes { get; set; }

    /// <summary>When the access token expires (null = non-expiring / unknown). Past → "expired".</summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime ConnectedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
