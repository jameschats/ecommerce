using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Shipping.Shiprocket;

/// <summary>Config for the Shiprocket courier-aggregator integration (app-level, config-gated).</summary>
public sealed class ShiprocketOptions
{
    public const string SectionName = "Shiprocket";
    public string Provider { get; set; } = "None";   // None | Shiprocket
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? PickupPincode { get; set; }
    public string BaseUrl { get; set; } = "https://apiv2.shiprocket.in/";
    public decimal DefaultWeightKg { get; set; } = 0.5m;
}

public sealed record ShiprocketRate(string CourierName, decimal Rate, int? EstimatedDays);

/// <summary>
/// Live courier rates from Shiprocket. Two implementations, selected by <c>Shiprocket:Provider</c>:
/// <see cref="NullShiprocketClient"/> (default — disabled) and <see cref="ShiprocketClient"/> (real HTTP).
/// Rate lookups are best-effort: any failure returns null so checkout falls back to the merchant's manual rates.
/// </summary>
public interface IShiprocketClient
{
    bool Enabled { get; }
    Task<ShiprocketRate?> GetCheapestRateAsync(string deliveryPincode, decimal weightKg, bool cod, CancellationToken ct = default);
}

/// <summary>Disabled default — no external calls; checkout uses manual shipping rates.</summary>
public sealed class NullShiprocketClient : IShiprocketClient
{
    public bool Enabled => false;
    public Task<ShiprocketRate?> GetCheapestRateAsync(string deliveryPincode, decimal weightKg, bool cod, CancellationToken ct = default)
        => Task.FromResult<ShiprocketRate?>(null);
}

/// <summary>
/// Real Shiprocket client: caches the auth token and queries courier serviceability for the
/// cheapest rate. Singleton so the token is shared. NOTE: app-level (single Shiprocket account);
/// per-tenant Shiprocket accounts + create-shipment/AWB/tracking-webhooks are a later live pass.
/// </summary>
public sealed class ShiprocketClient : IShiprocketClient
{
    private readonly HttpClient _http;
    private readonly ShiprocketOptions _opt;
    private readonly ILogger<ShiprocketClient> _log;
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private string? _token;
    private DateTime _tokenExpiresUtc;

    public ShiprocketClient(HttpClient http, IOptions<ShiprocketOptions> opt, ILogger<ShiprocketClient> log)
    {
        _opt = opt.Value;
        _http = http;
        _http.BaseAddress ??= new Uri(_opt.BaseUrl);
        _log = log;
    }

    public bool Enabled => true;

    public async Task<ShiprocketRate?> GetCheapestRateAsync(string deliveryPincode, decimal weightKg, bool cod, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.PickupPincode) || string.IsNullOrWhiteSpace(deliveryPincode))
            return null;
        var weight = weightKg > 0 ? weightKg : _opt.DefaultWeightKg;
        try
        {
            var token = await GetTokenAsync(ct);
            if (token is null) return null;

            var url = $"v1/external/courier/serviceability/?pickup_postcode={_opt.PickupPincode}"
                    + $"&delivery_postcode={deliveryPincode}&weight={weight}&cod={(cod ? 1 : 0)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized) _token = null;   // force re-auth next time
                _log.LogWarning("Shiprocket serviceability returned {Status}", (int)res.StatusCode);
                return null;
            }
            return ParseCheapest(await res.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Shiprocket rate lookup failed — falling back to manual shipping.");
            return null;
        }
    }

    /// <summary>Pick the cheapest serviceable courier from a Shiprocket serviceability response. Pure — unit-tested.</summary>
    public static ShiprocketRate? ParseCheapest(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty("available_courier_companies", out var couriers)
                || couriers.ValueKind != JsonValueKind.Array)
                return null;

            ShiprocketRate? best = null;
            foreach (var c in couriers.EnumerateArray())
            {
                if (!TryDecimal(c, "rate", out var rate)) continue;
                var name = c.TryGetProperty("courier_name", out var n) ? n.GetString() ?? "Courier" : "Courier";
                var days = TryInt(c, "estimated_delivery_days");
                if (best is null || rate < best.Rate) best = new ShiprocketRate(name, rate, days);
            }
            return best;
        }
        catch { return null; }
    }

    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTime.UtcNow < _tokenExpiresUtc) return _token;
        await _authLock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow < _tokenExpiresUtc) return _token;
            using var res = await _http.PostAsJsonAsync("v1/external/auth/login",
                new { email = _opt.Email, password = _opt.Password }, ct);
            if (!res.IsSuccessStatusCode) { _log.LogWarning("Shiprocket auth failed ({Status})", (int)res.StatusCode); return null; }
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            _token = doc.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
            _tokenExpiresUtc = DateTime.UtcNow.AddDays(9);   // Shiprocket tokens last ~10 days
            return _token;
        }
        finally { _authLock.Release(); }
    }

    private static bool TryDecimal(JsonElement el, string prop, out decimal value)
    {
        value = 0m;
        if (!el.TryGetProperty(prop, out var p)) return false;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out value)) return true;
        return p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), out value);
    }

    private static int? TryInt(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var p)) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i)) return i;
        return p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var s) ? s : null;
    }
}
