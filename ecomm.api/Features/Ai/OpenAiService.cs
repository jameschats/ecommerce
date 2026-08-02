using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ecomm.api.Common.Exceptions;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Ai;

/// <summary>
/// OpenAI Chat Completions provider. Activated when <c>Ai:Provider=OpenAI</c> and an API key is set.
/// Uses a cheap bulk model (<c>Ai:OpenAi:Model</c>, default gpt-5-mini) — the platform pays the bill,
/// merchants spend abstract credits. JSON mode is used for structured generation.
/// </summary>
public sealed class OpenAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _opt;
    private readonly ILogger<OpenAiService> _log;

    public OpenAiService(HttpClient http, IOptions<AiOptions> options, ILogger<OpenAiService> log)
    {
        _opt = options.Value.OpenAi;
        _log = log;
        _http = http;
        _http.BaseAddress ??= new Uri(_opt.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(_opt.TimeoutSeconds);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey);
    }

    public bool Enabled => true;

    public long EstimateCostMicros(AiCompletion c)
    {
        var inr = c.PromptTokens / 1_000_000m * _opt.InputUsdPerMTok * _opt.UsdToInr
                + c.CompletionTokens / 1_000_000m * _opt.OutputUsdPerMTok * _opt.UsdToInr;
        return (long)Math.Round(inr * 1_000_000m, MidpointRounding.AwayFromZero);
    }

    public async Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = _opt.Model,
            ["messages"] = new object[]
            {
                new { role = "system", content = prompt.System },
                new { role = "user", content = prompt.User },
            },
            ["max_completion_tokens"] = prompt.MaxTokens,
        };
        if (prompt.Json) payload["response_format"] = new { type = "json_object" };

        var body = JsonSerializer.Serialize(payload);
        using var resp = await _http.PostAsync("v1/chat/completions",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _log.LogWarning("OpenAI request failed ({Status}): {Body}", (int)resp.StatusCode, json);
            throw new AppException("The AI service is temporarily unavailable. Please try again.", 502);
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var choice = root.GetProperty("choices")[0];
        var text = choice.GetProperty("message").GetProperty("content").GetString() ?? "";
        var model = root.TryGetProperty("model", out var m) ? m.GetString() ?? _opt.Model : _opt.Model;

        // Truncation (hit the token cap) or empty content — common with reasoning models — would otherwise
        // surface downstream as an opaque JSON-parse error. Fail here with a message the merchant can act on.
        var finish = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;
        if (finish == "length")
        {
            _log.LogWarning("OpenAI response truncated (finish_reason=length, model {Model}, max_completion_tokens {Max}).", model, prompt.MaxTokens);
            throw new AppException("The response was too large to finish. Please try again with fewer categories or products.", 502);
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            _log.LogWarning("OpenAI returned empty content (finish_reason={Finish}, model {Model}).", finish, model);
            throw new AppException("The AI returned an empty response. Please try again.", 502);
        }
        int pt = 0, cmp = 0;
        if (root.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object)
        {
            if (u.TryGetProperty("prompt_tokens", out var p)) pt = p.GetInt32();
            if (u.TryGetProperty("completion_tokens", out var c)) cmp = c.GetInt32();
        }
        return new AiCompletion(text.Trim(), pt, cmp, model);
    }
}
