using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.WhatsApp;

/// <summary>
/// Real WhatsApp delivery via Gupshup's WhatsApp Business API. Activated by
/// <c>WhatsApp:Provider=Gupshup</c> with an API key and the business's registered source number.
/// <para><b>Unverified against a live account</b> — the vendor decision (Gupshup, see
/// phase-1-notifications-2fa.md Track C) was made 2026-08-21 before a real Gupshup business
/// account/WABA existed; that's an operational step (KYC, Meta business verification, template
/// approval — all take real calendar time), not something this code can do. This implementation
/// follows Gupshup's public API docs (console-docs.gupshup.io) as closely as available, but has
/// not been exercised against a live account/API key. Re-verify the exact header name and endpoint
/// once real credentials exist — most Gupshup v1 endpoints use an <c>apikey</c> header, but at
/// least one docs page showed <c>api_key</c> for the session-message endpoint, which may be a
/// documentation artifact rather than a real inconsistency; test both if the first send 401s.
/// </para>
/// </summary>
public sealed class GupshupWhatsAppProvider : IWhatsAppProvider
{
    private const string TemplateMsgUrl = "https://api.gupshup.io/sm/api/v1/template/msg";
    private const string SessionMsgUrl = "https://api.gupshup.io/sm/api/v1/msg";

    private readonly HttpClient _http;
    private readonly WhatsAppOptions _opts;
    private readonly ILogger<GupshupWhatsAppProvider> _logger;

    public GupshupWhatsAppProvider(HttpClient http, IOptions<WhatsAppOptions> opts, ILogger<GupshupWhatsAppProvider> logger)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<WhatsAppSendResult> SendTemplateMessageAsync(string toPhone, string templateId,
        IReadOnlyList<string> parameters, CancellationToken ct = default)
    {
        var destination = Normalize(toPhone);
        if (destination is null) return WhatsAppSendResult.Fail($"Could not normalize destination phone '{toPhone}'.");

        var templateJson = JsonSerializer.Serialize(new { id = templateId, @params = parameters });
        var form = new Dictionary<string, string>
        {
            ["source"] = _opts.SourceNumber,
            ["destination"] = destination,
            ["template"] = templateJson,
        };
        return await PostAsync(TemplateMsgUrl, form, ct);
    }

    public async Task<WhatsAppSendResult> SendSessionMessageAsync(string toPhone, string body, CancellationToken ct = default)
    {
        var destination = Normalize(toPhone);
        if (destination is null) return WhatsAppSendResult.Fail($"Could not normalize destination phone '{toPhone}'.");

        var messageJson = JsonSerializer.Serialize(new { type = "text", text = body });
        var form = new Dictionary<string, string>
        {
            ["channel"] = "whatsapp",
            ["source"] = _opts.SourceNumber,
            ["src.name"] = _opts.AppName,
            ["destination"] = destination,
            ["message"] = messageJson,
        };
        return await PostAsync(SessionMsgUrl, form, ct);
    }

    private async Task<WhatsAppSendResult> PostAsync(string url, Dictionary<string, string> form, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        req.Headers.TryAddWithoutValidation("apikey", _opts.ApiKey);

        try
        {
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("Gupshup send failed ({Status}): {Body}", (int)res.StatusCode, body);
                return WhatsAppSendResult.Fail($"HTTP {(int)res.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
            var messageId = doc.RootElement.TryGetProperty("messageId", out var m) ? m.GetString() : null;
            if (!string.Equals(status, "submitted", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Gupshup reported non-submitted status: {Body}", body);
                return WhatsAppSendResult.Fail($"Unexpected status: {body}");
            }
            return WhatsAppSendResult.Ok(messageId ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gupshup request threw.");
            return WhatsAppSendResult.Fail(ex.Message);
        }
    }

    /// <summary>Digits only; prefix the default Indian country code for bare 10-digit numbers —
    /// same normalization convention as <see cref="Auth.Services.Msg91SmsSender"/>.</summary>
    private static string? Normalize(string phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 10) digits = "91" + digits;
        return digits.Length is >= 11 and <= 15 ? digits : null;
    }
}
