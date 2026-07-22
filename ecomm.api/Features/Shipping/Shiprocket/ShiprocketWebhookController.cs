using System.Globalization;
using System.Text.Json;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Shipping.Shiprocket;

/// <summary>
/// Receives Shiprocket tracking webhooks (SR5). Anonymous; when <c>Shiprocket:WebhookToken</c> is
/// configured the <c>x-api-key</c> header must match (the token the merchant sets on their Shiprocket
/// webhook). The payload self-identifies the store via the AWB, so no tenant context is needed.
/// NOTE: the route deliberately avoids the words "shiprocket"/"sr" — Shiprocket rejects webhook URLs
/// containing those keywords.
/// </summary>
[ApiController]
[Route("api/webhooks/tracking")]
[AllowAnonymous]
public sealed class ShiprocketWebhookController(
    IShiprocketWebhookService svc, IConfiguration config, ILogger<ShiprocketWebhookController> log) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] JsonElement body, CancellationToken ct)
    {
        var token = config["Shiprocket:WebhookToken"];
        if (!string.IsNullOrEmpty(token) && Request.Headers["x-api-key"].ToString() != token)
            return Unauthorized(ApiResponse<object>.Fail("Invalid webhook token."));

        var awb = Str(body, "awb");
        var status = Str(body, "current_status") ?? Str(body, "shipment_status") ?? Str(body, "status");
        if (string.IsNullOrWhiteSpace(awb) || string.IsNullOrWhiteSpace(status))
            return Ok(ApiResponse<object>.Ok(new { handled = false }, "Ignored — missing awb/status."));

        // The payload already carries scan detail we used to throw away — keep it.
        var scan = new ShiprocketScan(
            Location: Str(body, "location") ?? Str(body, "current_location") ?? Str(body, "scan_location"),
            Remark: Str(body, "remark") ?? Str(body, "activity") ?? Str(body, "sr_status_label"),
            OccurredAt: DateTime.TryParse(
                Str(body, "scan_date") ?? Str(body, "current_timestamp") ?? Str(body, "timestamp"),
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var occurred) ? occurred : null,
            RawPayload: body.ValueKind == JsonValueKind.Object ? body.GetRawText() : null);

        var handled = await svc.HandleAsync(awb!, status!, scan, ct);
        log.LogInformation("Shiprocket webhook AWB {Awb} status '{Status}': {Outcome}", awb, status, handled ? "applied" : "no-match");
        return Ok(ApiResponse<object>.Ok(new { handled }));
    }

    private static string? Str(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var p)
            ? p.ValueKind switch { JsonValueKind.String => p.GetString(), JsonValueKind.Number => p.ToString(), _ => null }
            : null;
}
