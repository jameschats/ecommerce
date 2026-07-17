namespace ecomm.api.Features.Ai;

/// <summary>
/// AI provider configuration. Mirrors the Payments/Shiprocket config-gating: <c>Ai:Provider</c> selects
/// the implementation (None → <see cref="NullAiService"/>, OpenAI → <see cref="OpenAiService"/>).
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";
    public string Provider { get; set; } = "None";   // None | OpenAI
    public OpenAiOptions OpenAi { get; set; } = new();
}

public sealed class OpenAiOptions
{
    public string ApiKey { get; set; } = "";
    // Default to a NON-reasoning mini: cheap, fast, and reliably returns JSON. GPT-5 minis are reasoning
    // models that can spend a small token budget on hidden reasoning and return empty content in JSON mode,
    // which would break catalog/mapping/SEO/page generation. Override via Ai:OpenAi:Model if desired.
    public string Model { get; set; } = "gpt-4.1-mini";
    public string BaseUrl { get; set; } = "https://api.openai.com/";
    public int TimeoutSeconds { get; set; } = 60;

    // Real provider cost, used only to estimate AiUsageLog.CostMicros for margin tuning (not billing).
    public decimal InputUsdPerMTok { get; set; } = 0.15m;
    public decimal OutputUsdPerMTok { get; set; } = 0.60m;
    public decimal UsdToInr { get; set; } = 88m;
}
