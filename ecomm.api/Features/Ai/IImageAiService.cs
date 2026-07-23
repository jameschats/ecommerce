namespace ecomm.api.Features.Ai;

/// <summary>A prompt to generate a marketing image. Size is a hint the provider maps to its nearest supported value.</summary>
public sealed record ImagePrompt(string Prompt, string Size = "1024x1024");

/// <summary>
/// A generated image plus what it actually cost us. <see cref="CostMicros"/> is real provider spend in
/// ₹×1e6 — image generation costs real money per call (unlike text), so we record it to price credits.
/// </summary>
public sealed record ImageResult(byte[] Bytes, string ContentType, long CostMicros, string Model);

/// <summary>
/// Image generation, kept separate from <see cref="IAiService"/> (text) so the two provider stacks and
/// their (very different) costs stay independent. Config-gated by <c>Ai:ImageProvider</c> — None disables
/// it, OpenAI uses DALL·E today, and a Gemini/Imagen implementation drops in later as another value with
/// no caller changes.
/// </summary>
public interface IImageAiService
{
    bool Enabled { get; }
    Task<ImageResult> GenerateAsync(ImagePrompt prompt, CancellationToken ct = default);
}

/// <summary>The no-op used when <c>Ai:ImageProvider=None</c> (the shipped default). Any call is a 503.</summary>
public sealed class NullImageAiService : IImageAiService
{
    public bool Enabled => false;
    public Task<ImageResult> GenerateAsync(ImagePrompt prompt, CancellationToken ct = default) =>
        throw new Common.Exceptions.AppException("Image generation isn't enabled on this platform.", 503);
}
