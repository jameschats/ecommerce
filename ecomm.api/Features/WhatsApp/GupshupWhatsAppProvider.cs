using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.WhatsApp;

/// <summary>
/// Real WhatsApp delivery via Gupshup's <b>GatewayAPI/rest</b> (the smsGupshup "enterprise" line that
/// Conversation Cloud self-serve accounts actually expose under Integrations → APIs — NOT the
/// api.gupshup.io/sm/api/v1 self-serve line this class targeted before). Activated by
/// <c>WhatsApp:Provider=Gupshup</c> with the account's <b>Client ID</b> (<see cref="WhatsAppOptions.UserId"/>,
/// the <c>userid</c> field) and <b>Secret Token</b> (<see cref="WhatsAppOptions.ApiKey"/>, sent as a Bearer token).
/// <para>Contract confirmed 2026-08-28 from the live account's own API page
/// (console.gupshup.io/unified/apis): <c>POST {ApiBaseUrl}</c>, form-urlencoded,
/// <c>method=SendMessage&amp;msg_type=text&amp;auth_scheme=plain&amp;v=1.1&amp;format=json</c>; a pre-approved
/// template send sets <c>isHSM=true&amp;isTemplate=true&amp;whatsAppTemplateId=&lt;id&gt;</c> and passes body
/// placeholders as <c>var1,var2,…</c> (a plain no-variable template needs no <c>msg</c>). Success JSON is
/// <c>{"response":{"id","phone","details","status":"success"}}</c>.</para>
/// <para><b>Not yet exercised end-to-end against the live account</b> — pending the account's
/// <c>PENDING_INTERNAL_SETUP</c>/MM-Lite completion and a Meta-approved template. Re-verify on the first
/// real send: the exact <c>var</c> naming and whether a variable template also wants <c>msg</c>.</para>
/// </summary>
public sealed class GupshupWhatsAppProvider : IWhatsAppProvider
{
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

        var form = BaseForm(destination);
        form["isHSM"] = "true";
        form["isTemplate"] = "true";
        form["whatsAppTemplateId"] = templateId;
        // Body placeholders {{1}},{{2}},… map to var1,var2,… in the GatewayAPI.
        for (var i = 0; i < parameters.Count; i++) form[$"var{i + 1}"] = parameters[i] ?? "";
        return await PostAsync(form, ct);
    }

    public async Task<WhatsAppSendResult> SendSessionMessageAsync(string toPhone, string body, CancellationToken ct = default)
    {
        var destination = Normalize(toPhone);
        if (destination is null) return WhatsAppSendResult.Fail($"Could not normalize destination phone '{toPhone}'.");

        // Free-form (session) message: same endpoint, no HSM/template flags — just the text in `msg`.
        var form = BaseForm(destination);
        form["msg"] = body;
        return await PostAsync(form, ct);
    }

    /// <summary>Params common to every GatewayAPI SendMessage call. Credentials go in the body
    /// (<c>userid</c>) and header (Bearer <c>ApiKey</c>).</summary>
    private Dictionary<string, string> BaseForm(string destination) => new()
    {
        ["method"] = "SendMessage",
        ["userid"] = _opts.UserId,
        ["send_to"] = destination,
        ["msg_type"] = "text",
        ["auth_scheme"] = "plain",
        ["v"] = "1.1",
        ["format"] = "json",
    };

    private async Task<WhatsAppSendResult> PostAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _opts.ApiBaseUrl) { Content = new FormUrlEncodedContent(form) };
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_opts.ApiKey}");

        try
        {
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("Gupshup send failed ({Status}): {Body}", (int)res.StatusCode, body);
                return WhatsAppSendResult.Fail($"HTTP {(int)res.StatusCode}: {body}");
            }

            // { "response": { "id": "...", "phone": "...", "details": "...", "status": "success" } }
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("response", out var resp))
                return WhatsAppSendResult.Fail($"Unexpected response: {body}");

            var status = resp.TryGetProperty("status", out var s) ? s.GetString() : null;
            var messageId = resp.TryGetProperty("id", out var m) ? m.GetString() : null;
            if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Gupshup reported non-success status: {Body}", body);
                return WhatsAppSendResult.Fail($"Send not accepted: {body}");
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
