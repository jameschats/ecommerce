using System.Net;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Analytics;

public sealed class GeoIpOptions
{
    public const string SectionName = "GeoIp";
    /// <summary>Absolute path to a GeoLite2-City.mmdb file. Blank/missing ⇒ geo lookup no-ops.</summary>
    public string? DatabasePath { get; set; }
}

public interface IGeoLookupService
{
    (string? Country, string? City) Lookup(string? ipAddress);
}

/// <summary>
/// Resolves an IP to a country/city via a local MaxMind GeoLite2 database. The database file
/// is not part of this repo (a free MaxMind account + licence key is needed to download it —
/// see documents for the production setup note); until one is configured, every lookup
/// returns (null, null) rather than failing, so traffic tracking works with plain
/// device/source/page data and geo fills in once the file is in place.
/// </summary>
public sealed class GeoLookupService : IGeoLookupService, IDisposable
{
    private readonly DatabaseReader? _reader;
    private readonly ILogger<GeoLookupService> _logger;

    public GeoLookupService(IOptions<GeoIpOptions> options, ILogger<GeoLookupService> logger)
    {
        _logger = logger;
        var path = options.Value.DatabasePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogInformation("GeoIp:DatabasePath not configured — country/city traffic breakdown disabled.");
            return;
        }
        if (!File.Exists(path))
        {
            _logger.LogWarning("GeoIp database not found at {Path} — country/city traffic breakdown disabled.", path);
            return;
        }
        try
        {
            _reader = new DatabaseReader(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open GeoIp database at {Path} — country/city traffic breakdown disabled.", path);
        }
    }

    public (string? Country, string? City) Lookup(string? ipAddress)
    {
        if (_reader is null || string.IsNullOrWhiteSpace(ipAddress)) return (null, null);
        if (!IPAddress.TryParse(ipAddress, out var ip) || IsPrivate(ip)) return (null, null);

        try
        {
            var result = _reader.City(ip);
            return (result.Country.Name, result.City.Name);
        }
        catch
        {
            // Unmapped IP ranges (test ranges, some hosting blocks) throw AddressNotFoundException —
            // no different from "we don't know", so it isn't worth its own branch.
            return (null, null);
        }
    }

    /// <summary>Loopback/RFC1918/link-local — MaxMind has no data for these (dev, LAN, proxies).</summary>
    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254);
        }
        return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;
    }

    public void Dispose() => _reader?.Dispose();
}
