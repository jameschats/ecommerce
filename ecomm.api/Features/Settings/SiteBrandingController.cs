using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Settings;

public sealed record SiteBrandingDto(
    string BrowserTitle, string FaviconUrl, string SiteName, string LogoUrl,
    /// <summary>Tail of the wordmark shown in the primary colour, concatenated onto SiteName.</summary>
    string SiteNameAccent,
    /// <summary>Wordmark font size in rem. Empty keeps the built-in 1.25rem.</summary>
    string SiteNameSize,
    /// <summary>Logo for the dark footer. Empty means reuse LogoUrl.</summary>
    string FooterLogoUrl,
    /// <summary>Header announcement — seasonal booking notices, price validity and the like.</summary>
    string AnnouncementText, string PriceValidUpto);

/// <summary>
/// Public branding — the browser tab title and favicon, plus the storefront name and
/// header logo (design.md §10.3).
///
/// Anonymous and output-cached: every visitor gets the same handful of strings, and this is
/// fetched on the very first render, so it must not cost a database round trip per page.
/// </summary>
[ApiController]
[Route("api/site")]
[OutputCache(PolicyName = "public")]
public sealed class SiteBrandingController : ControllerBase
{
    private readonly EcommerceDbContext _db;

    public SiteBrandingController(EcommerceDbContext db) => _db = db;

    [HttpGet("branding")]
    public async Task<IActionResult> Branding(CancellationToken ct)
    {
        var rows = await _db.Settings
            .Where(s => s.SettingKey == "Site.BrowserTitle" || s.SettingKey == "Site.FaviconUrl"
                     || s.SettingKey == "Site.Name" || s.SettingKey == "Site.LogoUrl"
                     || s.SettingKey == "Site.NameAccent" || s.SettingKey == "Site.FooterLogoUrl"
                     || s.SettingKey == "Site.NameSize"
                     || s.SettingKey == "QuickOrder.AnnouncementText"
                     || s.SettingKey == "QuickOrder.PriceValidUpto")
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? "", ct);

        string Get(string key) => rows.TryGetValue(key, out var v) ? v : "";

        return Ok(ApiResponse<SiteBrandingDto>.Ok(new SiteBrandingDto(
            Get("Site.BrowserTitle"),
            Get("Site.FaviconUrl"),
            Get("Site.Name"),
            Get("Site.LogoUrl"),
            Get("Site.NameAccent"),
            Get("Site.NameSize"),
            Get("Site.FooterLogoUrl"),
            Get("QuickOrder.AnnouncementText"),
            Get("QuickOrder.PriceValidUpto"))));
    }
}
