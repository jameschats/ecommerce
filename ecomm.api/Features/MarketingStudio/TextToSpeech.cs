using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>Synthesised audio: raw bytes + how to store/serve them.</summary>
public sealed record TtsAudio(byte[] Bytes, string Mime, string Extension);

/// <summary>Text-to-speech for video voiceovers (MS3). India-first via Sarvam; a Null provider stands
/// in when no key is configured (same dev-provider convention as Email/SMS/WhatsApp).</summary>
public interface ITextToSpeech
{
    bool Enabled { get; }
    /// <summary>Synthesise <paramref name="text"/> in <paramref name="languageCode"/> (e.g. hi-IN); null on
    /// failure or when disabled.</summary>
    Task<TtsAudio?> SynthesizeAsync(string text, string languageCode, string? speaker, CancellationToken ct = default);
}

/// <summary>Config section "Sarvam" — the TTS subscription key (kept in env/user-secrets, never in git).</summary>
public sealed class SarvamOptions
{
    public const string SectionName = "Sarvam";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "bulbul:v2";
    public string BaseUrl { get; set; } = "https://api.sarvam.ai";
}

/// <summary>
/// Sarvam AI text-to-speech (POST /text-to-speech, header <c>api-subscription-key</c>). Returns MP3
/// bytes decoded from the base64 <c>audios[0]</c>. Disabled (no-op) when no key is set. India-first:
/// Hindi/Tamil/Telugu/Kannada/Malayalam and more.
/// </summary>
public sealed class SarvamTextToSpeech(IHttpClientFactory httpFactory, IOptions<SarvamOptions> options, ILogger<SarvamTextToSpeech> log)
    : ITextToSpeech
{
    private readonly SarvamOptions _opt = options.Value;
    public bool Enabled => !string.IsNullOrWhiteSpace(_opt.ApiKey);

    public async Task<TtsAudio?> SynthesizeAsync(string text, string languageCode, string? speaker, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(text)) return null;
        var body = new Dictionary<string, object?>
        {
            ["text"] = text.Length > 1500 ? text[..1500] : text,
            ["language_code"] = languageCode,
            ["model"] = _opt.Model,
            ["output_audio_codec"] = "mp3",
        };
        if (!string.IsNullOrWhiteSpace(speaker)) body["speaker"] = speaker;

        try
        {
            var http = httpFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_opt.BaseUrl.TrimEnd('/')}/text-to-speech")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("api-subscription-key", _opt.ApiKey);
            using var resp = await http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) { log.LogWarning("Sarvam TTS failed ({Code}): {Body}", (int)resp.StatusCode, Trim(json)); return null; }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("audios", out var audios) || audios.ValueKind != JsonValueKind.Array || audios.GetArrayLength() == 0)
                return null;
            var b64 = audios[0].GetString();
            if (string.IsNullOrEmpty(b64)) return null;
            return new TtsAudio(Convert.FromBase64String(b64), "audio/mpeg", "mp3");
        }
        catch (Exception ex) { log.LogWarning(ex, "Sarvam TTS call errored."); return null; }
    }

    private static string Trim(string s) => s.Length <= 300 ? s : s[..300];
}

/// <summary>Dev/default TTS — no key, no audio. Selected when <c>Sarvam:ApiKey</c> is blank.</summary>
public sealed class NullTextToSpeech : ITextToSpeech
{
    public bool Enabled => false;
    public Task<TtsAudio?> SynthesizeAsync(string text, string languageCode, string? speaker, CancellationToken ct = default) =>
        Task.FromResult<TtsAudio?>(null);
}
