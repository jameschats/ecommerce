using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Auth.Services;

/// <summary>
/// Real SMS delivery via MSG91's Flow API (v5). Activated by <c>Sms:Provider=Msg91</c> with an
/// auth key, a DLT-registered sender id, and a flow <c>TemplateId</c> whose single variable
/// (<c>VariableName</c>, default "body") carries the message text.
/// <para>India note: transactional SMS requires DLT registration — the sender id + template must be
/// approved on the DLT portal before messages deliver, otherwise carriers drop them.</para>
/// </summary>
public sealed class Msg91SmsSender : ISmsSender
{
    private const string FlowUrl = "https://control.msg91.com/api/v5/flow/";

    private readonly HttpClient _http;
    private readonly SmsOptions _opts;
    private readonly ILogger<Msg91SmsSender> _logger;

    public Msg91SmsSender(HttpClient http, IOptions<SmsOptions> opts, ILogger<Msg91SmsSender> logger)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        var mobile = Normalize(phoneNumber);
        if (mobile is null)
        {
            _logger.LogWarning("MSG91: skipping SMS — could not normalize phone '{Phone}'.", phoneNumber);
            return;
        }

        var payload = new
        {
            template_id = _opts.TemplateId,
            sender = _opts.SenderId,
            short_url = "0",
            recipients = new[]
            {
                new Dictionary<string, string> { ["mobiles"] = mobile, [_opts.VariableName] = message },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, FlowUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        req.Headers.TryAddWithoutValidation("authkey", _opts.AuthKey);

        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode || body.Contains("\"type\":\"error\"", StringComparison.OrdinalIgnoreCase))
            _logger.LogError("MSG91 send failed ({Status}) to {Mobile}: {Body}", (int)res.StatusCode, mobile, body);
        else
            _logger.LogInformation("MSG91 SMS accepted for {Mobile}.", mobile);
    }

    /// <summary>Digits only; prefix the default country code for bare 10-digit Indian numbers.</summary>
    private string? Normalize(string phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 10) digits = _opts.CountryCode + digits;
        return digits.Length is >= 11 and <= 15 ? digits : null;
    }
}
