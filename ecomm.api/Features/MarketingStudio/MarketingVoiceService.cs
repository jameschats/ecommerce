using ecomm.api.Common.Exceptions;
using ecomm.api.Features.Media;
using Microsoft.AspNetCore.Http;

namespace ecomm.api.Features.MarketingStudio;

public sealed record NamedCode(string Code, string Name);
public sealed record VoiceOptionsDto(bool Enabled, IReadOnlyList<NamedCode> Languages, IReadOnlyList<NamedCode> Speakers);
public sealed record VoicePreviewRequest(string Text, string LanguageCode, string? Speaker);
public sealed record VoicePreviewResult(string AudioUrl);

public interface IMarketingVoiceService
{
    VoiceOptionsDto Options();
    Task<VoicePreviewResult> PreviewAsync(VoicePreviewRequest req, CancellationToken ct = default);
}

/// <summary>
/// Voiceover generation for the Marketing Studio (MS3·a). Wraps <see cref="ITextToSpeech"/> (Sarvam,
/// India-first) — synthesises a phrase and stores the MP3 via media storage so it can be previewed now
/// and, later, muxed into a reel by the render worker. Disabled cleanly (clear 409) when no TTS key is
/// configured, so nothing breaks without one.
/// </summary>
public sealed class MarketingVoiceService(ITextToSpeech tts, IMediaStorage media) : IMarketingVoiceService
{
    // Sarvam-supported Indian languages (language_code enum).
    private static readonly IReadOnlyList<NamedCode> Langs = new[]
    {
        new NamedCode("en-IN", "English"), new NamedCode("hi-IN", "Hindi"), new NamedCode("ta-IN", "Tamil"),
        new NamedCode("te-IN", "Telugu"), new NamedCode("kn-IN", "Kannada"), new NamedCode("ml-IN", "Malayalam"),
        new NamedCode("mr-IN", "Marathi"), new NamedCode("bn-IN", "Bengali"), new NamedCode("gu-IN", "Gujarati"),
        new NamedCode("pa-IN", "Punjabi"), new NamedCode("od-IN", "Odia"),
    };
    // Kept minimal + safe: "Auto" (API default) always works; named voices are refined as we validate them.
    private static readonly IReadOnlyList<NamedCode> Speakers = new[]
    {
        new NamedCode("", "Auto (recommended)"), new NamedCode("anushka", "Anushka (female)"), new NamedCode("abhilash", "Abhilash (male)"),
    };

    public VoiceOptionsDto Options() => new(tts.Enabled, Langs, Speakers);

    public async Task<VoicePreviewResult> PreviewAsync(VoicePreviewRequest req, CancellationToken ct = default)
    {
        if (!tts.Enabled)
            throw new AppException("Voice generation isn't set up on this store yet.", StatusCodes.Status409Conflict);
        if (string.IsNullOrWhiteSpace(req.Text))
            throw new AppException("Enter some text to voice.", StatusCodes.Status400BadRequest);

        var lang = Langs.Any(l => l.Code == req.LanguageCode) ? req.LanguageCode : "en-IN";
        var speaker = string.IsNullOrWhiteSpace(req.Speaker) ? null : req.Speaker;

        var audio = await tts.SynthesizeAsync(req.Text.Trim(), lang, speaker, ct);
        if (audio is null)
            throw new AppException("Couldn't generate the voiceover just now. Please try again.", StatusCodes.Status502BadGateway);

        var stored = await media.SaveAsync(new MemoryStream(audio.Bytes), $"voiceover.{audio.Extension}", audio.Mime, ct);
        return new VoicePreviewResult(stored.Url);
    }
}
