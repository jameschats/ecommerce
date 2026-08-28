namespace ecomm.api.Features.WhatsApp;

/// <summary>Config section "WhatsApp" — same provider-selection pattern as Email/SMS/Ai.</summary>
public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>"None" (default, uses <see cref="LoggingWhatsAppProvider"/>), "Gupshup", or "Interakt".</summary>
    public string Provider { get; set; } = "None";

    /// <summary>Auth secret. For Gupshup's GatewayAPI it's the <b>Secret Token</b> (sent as
    /// <c>Authorization: Bearer</c>); for Interakt it's the API key.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Gupshup GatewayAPI <b>Client ID</b> (the <c>userid</c> form field, e.g. "2000270417").
    /// Shown in Conversation Cloud under Integrations → APIs. Not used by Interakt.</summary>
    public string UserId { get; set; } = "";

    /// <summary>Gupshup GatewayAPI base URL. Overridable per account/region; the Conversation Cloud
    /// self-serve default is media/enterprise smsGupshup.</summary>
    public string ApiBaseUrl { get; set; } = "https://mediaapi.smsgupshup.com/GatewayAPI/rest";

    /// <summary>The business's own WhatsApp-registered number, E.164 without a leading '+' (e.g. "918921225306").
    /// Informational for the GatewayAPI (sender is bound to <see cref="UserId"/>); used by other providers.</summary>
    public string SourceNumber { get; set; } = "";

    /// <summary>Gupshup app name (self-serve api.gupshup.io line only). Unused by the GatewayAPI.</summary>
    public string AppName { get; set; } = "";
}
