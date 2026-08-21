using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.WhatsApp;

/// <summary>
/// Real WhatsApp delivery via Interakt's API. Activated by <c>WhatsApp:Provider=Interakt</c> with an
/// API key — the fallback candidate from the Track C vendor comparison (phase-1-notifications-2fa.md),
/// evaluated when Gupshup's own signup flow had operational problems. Interakt's onboarding is simpler
/// (single-business dashboard, not a partner/multi-tenant program), which is exactly the tradeoff that
/// comparison flagged: easier to get running now, a worse long-term fit once WavCommerce needs to
/// provision a WABA per merchant tenant rather than one account for the platform itself.
/// <para><b>Unverified against a live account</b>, same posture as <see cref="GupshupWhatsAppProvider"/>.
/// The template-message contract below is taken from Interakt's own published docs (POST
/// <c>https://api.interakt.ai/v1/public/message/</c>, HTTP Basic auth, <c>{"result","message","id"}</c>
/// response). The session/free-form message shape below is <b>inferred</b>, not documented — Interakt's
/// public docs only cover the template endpoint; confirm the exact free-form JSON shape (field names
/// under "type":"Text") against a real account before relying on it.</para>
/// </summary>
public sealed class InteraktWhatsAppProvider : IWhatsAppProvider
{
    private const string MessageUrl = "https://api.interakt.ai/v1/public/message/";

    private readonly HttpClient _http;
    private readonly WhatsAppOptions _opts;
    private readonly ILogger<InteraktWhatsAppProvider> _logger;

    public InteraktWhatsAppProvider(HttpClient http, IOptions<WhatsAppOptions> opts, ILogger<InteraktWhatsAppProvider> logger)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
    }

    public Task<WhatsAppSendResult> SendTemplateMessageAsync(string toPhone, string templateId,
        IReadOnlyList<string> parameters, CancellationToken ct = default)
    {
        var (countryCode, number) = Normalize(toPhone);
        if (countryCode is null) return Task.FromResult(WhatsAppSendResult.Fail($"Could not normalize destination phone '{toPhone}'."));

        var payload = new
        {
            countryCode,
            phoneNumber = number,
            type = "Template",
            template = new { name = templateId, languageCode = "en", bodyValues = parameters },
        };
        return PostAsync(payload, ct);
    }

    public Task<WhatsAppSendResult> SendSessionMessageAsync(string toPhone, string body, CancellationToken ct = default)
    {
        var (countryCode, number) = Normalize(toPhone);
        if (countryCode is null) return Task.FromResult(WhatsAppSendResult.Fail($"Could not normalize destination phone '{toPhone}'."));

        // Inferred shape — see the class doc comment. Not exercised by Track A/B (they only send
        // template messages); this exists for Phase 2's future chatbot WhatsApp channel.
        var payload = new { countryCode, phoneNumber = number, type = "Text", data = new { message = body } };
        return PostAsync(payload, ct);
    }

    private async Task<WhatsAppSendResult> PostAsync(object payload, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, MessageUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        req.Headers.TryAddWithoutValidation("Authorization", $"Basic {_opts.ApiKey}");

        try
        {
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("Interakt send failed ({Status}): {Body}", (int)res.StatusCode, body);
                return WhatsAppSendResult.Fail($"HTTP {(int)res.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var ok = doc.RootElement.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.True;
            var id = doc.RootElement.TryGetProperty("id", out var i) ? i.GetString() : null;
            if (!ok)
            {
                _logger.LogError("Interakt reported failure: {Body}", body);
                return WhatsAppSendResult.Fail($"Unexpected response: {body}");
            }
            return WhatsAppSendResult.Ok(id ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Interakt request threw.");
            return WhatsAppSendResult.Fail(ex.Message);
        }
    }

    /// <summary>Interakt wants country code and number as separate fields (unlike Gupshup's single
    /// E.164 field) — India-only normalization for now, same simplification used throughout this
    /// codebase's other phone-based senders (Gupshup's own provider, MSG91 SMS).</summary>
    private static (string? countryCode, string? number) Normalize(string phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 10) return ("+91", digits);
        if (digits.Length == 12 && digits.StartsWith("91")) return ("+91", digits[2..]);
        return (null, null);
    }
}
