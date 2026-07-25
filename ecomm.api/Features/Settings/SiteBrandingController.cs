using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Settings;

public sealed record SiteBrandingDto(string BrowserTitle, string FaviconUrl, string SiteName, string LogoUrl);

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
                     || s.SettingKey == "Site.Name" || s.SettingKey == "Site.LogoUrl")
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? "", ct);

        string Get(string key) => rows.TryGetValue(key, out var v) ? v : "";

        return Ok(ApiResponse<SiteBrandingDto>.Ok(new SiteBrandingDto(
            Get("Site.BrowserTitle"),
            Get("Site.FaviconUrl"),
            Get("Site.Name"),
            Get("Site.LogoUrl"))));
    }
}
