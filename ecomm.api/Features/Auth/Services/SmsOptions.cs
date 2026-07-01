namespace ecomm.api.Features.Auth.Services;

/// <summary>Bound from the <c>Sms</c> config section. Provider selects the <c>ISmsSender</c> impl.</summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary><c>Logging</c> (dev — writes to the log) or <c>Msg91</c> (real delivery).</summary>
    public string Provider { get; set; } = "Logging";

    // --- MSG91 (used when Provider = Msg91) ---
    public string AuthKey { get; set; } = "";
    /// <summary>DLT-registered 6-char sender/header, e.g. "CALSHP".</summary>
    public string SenderId { get; set; } = "";
    /// <summary>DLT flow template id the message is sent through (single body variable).</summary>
    public string TemplateId { get; set; } = "";
    /// <summary>Template variable name that carries the message text (matches the DLT template).</summary>
    public string VariableName { get; set; } = "body";
    /// <summary>Default country code prefixed to 10-digit numbers.</summary>
    public string CountryCode { get; set; } = "91";
}
