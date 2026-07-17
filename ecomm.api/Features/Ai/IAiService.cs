namespace ecomm.api.Features.Ai;

/// <summary>A single AI generation request. When <c>Json</c> is true the provider is asked to return a
/// strict JSON object (used by catalog generation, column mapping and page generation).</summary>
public sealed record AiPrompt(string System, string User, bool Json = false, int MaxTokens = 1024);

/// <summary>The provider's answer plus token usage (for credit-cost tuning and the usage ledger).</summary>
public sealed record AiCompletion(string Text, int PromptTokens, int CompletionTokens, string Model)
{
    public int TotalTokens => PromptTokens + CompletionTokens;
}

/// <summary>
/// Provider-agnostic AI text generation. Exactly one config-gated implementation is registered
/// (<see cref="OpenAiService"/> when <c>Ai:Provider=OpenAI</c>, else <see cref="NullAiService"/>).
/// Callers never see the provider — swap it without touching them, mirroring Payments/Shiprocket.
/// </summary>
public interface IAiService
{
    /// <summary>True when a real provider is configured. AI affordances hide when false.</summary>
    bool Enabled { get; }

    /// <summary>Estimated real provider cost (₹ ×1e6) for a completion — for margin tuning, not billing.</summary>
    long EstimateCostMicros(AiCompletion completion);

    Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default);
}
