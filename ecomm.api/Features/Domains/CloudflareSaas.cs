using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Domains;

/// <summary>Config section "Cloudflare" — Cloudflare-for-SaaS custom-hostname provisioning.</summary>
public sealed class CloudflareOptions
{
    public const string SectionName = "Cloudflare";
    public string ApiToken { get; set; } = "";
    public string ZoneId { get; set; } = "";
    /// <summary>The proxied hostname in our zone that merchants CNAME to (the SaaS fallback origin),
    /// e.g. "saas-origin.wavcommerce.online".</summary>
    public string CnameTarget { get; set; } = "";
}

/// <summary>Current state of a Cloudflare custom hostname. <see cref="SslStatus"/> reaches "active"
/// once the edge certificate is issued.</summary>
public sealed record CustomHostnameStatus(string Id, string SslStatus, string HostnameStatus);

/// <summary>
/// Thin wrapper over Cloudflare-for-SaaS Custom Hostnames. When a merchant connects a domain we create
/// a custom hostname; Cloudflare then validates control, issues + auto-renews the edge TLS cert, and
/// proxies the domain to our fallback origin. No-op (returns null) when not configured.
/// </summary>
public interface ICloudflareSaas
{
    bool Enabled { get; }
    /// <summary>The hostname merchants should CNAME their domain to (the fallback origin), or null.</summary>
    string? CnameTarget { get; }
    Task<CustomHostnameStatus?> EnsureAsync(string hostname, CancellationToken ct = default);
    Task<CustomHostnameStatus?> GetAsync(string hostname, CancellationToken ct = default);
    Task DeleteAsync(string hostname, CancellationToken ct = default);
}

public sealed class CloudflareSaas(HttpClient http, IOptions<CloudflareOptions> options, ILogger<CloudflareSaas> log) : ICloudflareSaas
{
    private readonly CloudflareOptions _o = options.Value;

    public bool Enabled => !string.IsNullOrWhiteSpace(_o.ApiToken) && !string.IsNullOrWhiteSpace(_o.ZoneId);
    public string? CnameTarget => string.IsNullOrWhiteSpace(_o.CnameTarget) ? null : _o.CnameTarget;

    private string Base => $"https://api.cloudflare.com/client/v4/zones/{_o.ZoneId}/custom_hostnames";

    public async Task<CustomHostnameStatus?> GetAsync(string hostname, CancellationToken ct = default)
    {
        if (!Enabled) return null;
        var body = await SendAsync(Req(HttpMethod.Get, $"{Base}?hostname={Uri.EscapeDataString(hostname)}"), ct);
        if (body is null) return null;
        using var doc = JsonDocument.Parse(body);
        var result = doc.RootElement.GetProperty("result");
        return result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0 ? Parse(result[0]) : null;
    }

    public async Task<CustomHostnameStatus?> EnsureAsync(string hostname, CancellationToken ct = default)
    {
        if (!Enabled) return null;
        var existing = await GetAsync(hostname, ct);
        if (existing is not null) return existing;

        // HTTP DCV works once the merchant's CNAME points at our proxied fallback origin.
        var req = Req(HttpMethod.Post, Base);
        req.Content = JsonContent.Create(new { hostname, ssl = new { method = "http", type = "dv" } });
        var body = await SendAsync(req, ct);
        if (body is null) return null;
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.Object ? Parse(r) : null;
    }

    public async Task DeleteAsync(string hostname, CancellationToken ct = default)
    {
        if (!Enabled) return;
        var existing = await GetAsync(hostname, ct);
        if (existing is null || string.IsNullOrEmpty(existing.Id)) return;
        await SendAsync(Req(HttpMethod.Delete, $"{Base}/{existing.Id}"), ct);
    }

    private HttpRequestMessage Req(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_o.ApiToken}");
        return req;
    }

    private async Task<string?> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        try
        {
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) { log.LogWarning("Cloudflare SaaS {Status}: {Body}", (int)res.StatusCode, body); return null; }
            return body;
        }
        catch (Exception ex) { log.LogWarning(ex, "Cloudflare SaaS request failed."); return null; }
    }

    private static CustomHostnameStatus Parse(JsonElement r)
    {
        var id = r.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
        var hostnameStatus = r.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
        var sslStatus = r.TryGetProperty("ssl", out var ssl) && ssl.ValueKind == JsonValueKind.Object
            && ssl.TryGetProperty("status", out var ss) ? ss.GetString() ?? "" : "";
        return new CustomHostnameStatus(id, sslStatus, hostnameStatus);
    }
}
