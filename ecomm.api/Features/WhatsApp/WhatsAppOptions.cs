namespace ecomm.api.Features.WhatsApp;

/// <summary>Config section "WhatsApp" — same provider-selection pattern as Email/SMS/Ai.</summary>
public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>"None" (default, uses <see cref="LoggingWhatsAppProvider"/>) or "Gupshup".</summary>
    public string Provider { get; set; } = "None";
    public string ApiKey { get; set; } = "";
    /// <summary>The business's own WhatsApp-registered number, E.164 without a leading '+' (e.g. "917834811114").</summary>
    public string SourceNumber { get; set; } = "";
    /// <summary>Gupshup app name, required for session (free-form) messages.</summary>
    public string AppName { get; set; } = "";
}
