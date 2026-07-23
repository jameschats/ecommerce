using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ecomm.api.Common.Exceptions;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Ai;

/// <summary>
/// OpenAI image generation (DALL·E). Activated when <c>Ai:ImageProvider=OpenAI</c>. Returns the raw
/// bytes so the caller can store them wherever it likes, plus the real rupee cost of the call — image
/// generation is ~100× the cost of a text completion, so that number drives credit pricing and the
/// per-tenant caps the design calls for.
/// </summary>
public sealed class OpenAiImageService : IImageAiService
{
    private readonly HttpClient _http;
    private readonly ImageAiOptions _opt;
    private readonly ILogger<OpenAiImageService> _log;

    public OpenAiImageService(HttpClient http, IOptions<AiOptions> options, ILogger<OpenAiImageService> log)
    {
        _opt = options.Value.Image;
        _log = log;
        _http = http;
        _http.BaseAddress ??= new Uri(_opt.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(_opt.TimeoutSeconds);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey);
    }

    public bool Enabled => true;

    public async Task<ImageResult> GenerateAsync(ImagePrompt prompt, CancellationToken ct = default)
    {
        var size = Normalize(prompt.Size);
        // No response_format: the newer image models reject it (they return b64 by default), while
        // dall-e returns a URL. We handle whichever comes back, so this works across models.
        var payload = JsonSerializer.Serialize(new
        {
            model = _opt.Model,
            prompt = prompt.Prompt,
            n = 1,
            size,
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/images/generations")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _log.LogWarning("OpenAI image request failed ({Status}): {Body}", (int)resp.StatusCode, body);
            throw new AppException("The image provider couldn't generate that. Try a simpler prompt.", 502);
        }

        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement.GetProperty("data")[0];

        byte[] bytes;
        if (first.TryGetProperty("b64_json", out var b64El) && b64El.GetString() is { Length: > 0 } b64)
            bytes = Convert.FromBase64String(b64);
        else if (first.TryGetProperty("url", out var urlEl) && urlEl.GetString() is { Length: > 0 } imageUrl)
            bytes = await _http.GetByteArrayAsync(imageUrl, ct);
        else
            throw new AppException("The image provider returned no image.", 502);

        var costMicros = (long)(CostUsd(size) * _opt.UsdToInr * 1_000_000m);
        return new ImageResult(bytes, "image/png", costMicros, _opt.Model);
    }

    /// <summary>
    /// gpt-image-1 supports 1024x1024, 1024x1536 (portrait) and 1536x1024 (landscape). Accept those,
    /// map aspect hints, and fall back to the square. (dall-e-3's 1792 sizes are auto-mapped too.)
    /// </summary>
    private static string Normalize(string? size) => size switch
    {
        "1024x1024" or "1024x1536" or "1536x1024" => size,
        "1024x1792" or "9:16" => "1024x1536",
        "1792x1024" or "16:9" => "1536x1024",
        _ => "1024x1024",
    };

    /// <summary>List price by shape, so <c>CostMicros</c> reflects real spend.</summary>
    private decimal CostUsd(string size) => size == "1024x1024" ? _opt.SquareUsd : _opt.WideUsd;
}
