namespace ecomm.api.Features.Ai;

/// <summary>
/// AI provider configuration. Mirrors the Payments/Shiprocket config-gating: <c>Ai:Provider</c> selects
/// the implementation (None → <see cref="NullAiService"/>, OpenAI → <see cref="OpenAiService"/>).
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";
    public string Provider { get; set; } = "None";        // None | OpenAI (text)
    public string ImageProvider { get; set; } = "None";   // None | OpenAI (images) — Gemini/Imagen later
    public OpenAiOptions OpenAi { get; set; } = new();
    public ImageAiOptions Image { get; set; } = new();
}

/// <summary>
/// Image-generation config. Separate from <see cref="OpenAiOptions"/> (text) because the model, the
/// endpoint and — most of all — the cost are on a different scale. Defaults reuse the OpenAI text key
/// unless <c>Ai:Image:ApiKey</c> is set, so enabling images is usually just <c>Ai:ImageProvider=OpenAI</c>.
/// </summary>
public sealed class ImageAiOptions
{
    public string ApiKey { get; set; } = "";
    // gpt-image-1 is OpenAI's current image model — strong at text and complex scenes, and the format-
    // agnostic parser also handles dall-e-3 if an account only has that. Override via Ai:Image:Model.
    public string Model { get; set; } = "gpt-image-1";
    public string BaseUrl { get; set; } = "https://api.openai.com/";
    public int TimeoutSeconds { get; set; } = 120;

    // Approx list prices (USD), used only to record real spend in CostMicros for margin tuning. gpt-image-1
    // has quality tiers (low/medium/high); these track medium and should be re-confirmed against the bill.
    public decimal SquareUsd { get; set; } = 0.040m;   // 1024x1024
    public decimal WideUsd { get; set; } = 0.060m;     // 1024x1536 / 1536x1024
    public decimal UsdToInr { get; set; } = 88m;
}

public sealed class OpenAiOptions
{
    public string ApiKey { get; set; } = "";
    // Default to a NON-reasoning mini: cheap, fast, and reliably returns JSON. GPT-5 minis are reasoning
    // models that can spend a small token budget on hidden reasoning and return empty content in JSON mode,
    // which would break catalog/mapping/SEO/page generation. Override via Ai:OpenAi:Model if desired.
    public string Model { get; set; } = "gpt-4.1-mini";
    public string BaseUrl { get; set; } = "https://api.openai.com/";
    // 60s was too tight for large structured completions (e.g. a multi-category sample-catalog
    // generation) — a big JSON response from a mini model can legitimately take 60-90s, which was
    // surfacing as an unhandled TaskCanceledException/500 on bigger requests. 120s matches the image
    // tier below and stays well under the reverse proxy's 300s read timeout.
    public int TimeoutSeconds { get; set; } = 120;

    // Real provider cost, used only to estimate AiUsageLog.CostMicros for margin tuning (not billing).
    public decimal InputUsdPerMTok { get; set; } = 0.15m;
    public decimal OutputUsdPerMTok { get; set; } = 0.60m;
    public decimal UsdToInr { get; set; } = 88m;
}
