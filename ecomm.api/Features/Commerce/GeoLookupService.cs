using System.Net;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;

namespace ecomm.api.Features.Commerce;

public interface IGeoLookupService
{
    bool Enabled { get; }
    /// <summary>Resolve an IP to (ISO country, region/state, city). Any part may be null; all null when
    /// disabled, unparseable, or not found.</summary>
    (string? Country, string? Region, string? City) Lookup(string? ip);
}

/// <summary>
/// Server-side IP geolocation via MaxMind GeoLite2-City, local-database mode (no network call, no
/// credentials at runtime). Config: <c>GeoIp:DatabasePath</c> pointing at a <c>GeoLite2-City.mmdb</c>
/// file. Blank/missing path → disabled (no-op), the correct default in dev. Singleton: the reader is
/// thread-safe and opened once. Mirrors the dailycalendarshop setup.
///
/// The .mmdb file is NOT in the repo (it needs a free MaxMind account to download + a licence-key
/// `geoipupdate` cron to refresh, published twice weekly). Place it on the server and set the path.
/// </summary>
public sealed class GeoLookupService : IGeoLookupService, IDisposable
{
    private readonly DatabaseReader? _reader;

    public GeoLookupService(IConfiguration config, ILogger<GeoLookupService> log)
    {
        var path = config["GeoIp:DatabasePath"];
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path)) { log.LogWarning("GeoIp:DatabasePath set but file not found: {Path} — geo lookups disabled.", path); return; }
        try { _reader = new DatabaseReader(path); log.LogInformation("GeoIP database loaded from {Path}.", path); }
        catch (Exception ex) { log.LogWarning(ex, "Failed to open GeoIP database at {Path} — geo lookups disabled.", path); }
    }

    public bool Enabled => _reader is not null;

    public (string? Country, string? Region, string? City) Lookup(string? ip)
    {
        if (_reader is null || string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out var addr)) return (null, null, null);
        try
        {
            var r = _reader.City(addr);
            return (r.Country?.IsoCode, r.MostSpecificSubdivision?.Name, r.City?.Name);
        }
        catch (AddressNotFoundException) { return (null, null, null); }
        catch (Exception) { return (null, null, null); }
    }

    public void Dispose() => _reader?.Dispose();
}
