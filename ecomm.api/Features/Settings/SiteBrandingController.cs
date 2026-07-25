using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Settings;

public sealed record SiteBrandingDto(string BrowserTitle, string FaviconUrl);

/// <summary>
/// Public branding — the browser tab title and favicon (design.md §10.3).
///
/// Anonymous and output-cached: every visitor gets the same two strings, and this is
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
            .Where(s => s.SettingKey == "Site.BrowserTitle" || s.SettingKey == "Site.FaviconUrl")
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? "", ct);

        return Ok(ApiResponse<SiteBrandingDto>.Ok(new SiteBrandingDto(
            rows.TryGetValue("Site.BrowserTitle", out var t) ? t : "",
            rows.TryGetValue("Site.FaviconUrl", out var f) ? f : "")));
    }
}
