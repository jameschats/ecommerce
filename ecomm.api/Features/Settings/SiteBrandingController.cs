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
    /// <summary>Replaces the hardcoded "Custom 2026 calendars..." blurb under the footer logo.</summary>
    string FooterDescription,
    /// <summary>Header announcement — seasonal booking notices, price validity and the like.</summary>
    string AnnouncementText, string PriceValidUpto,
    /// <summary>
    /// How to reach the shop. Served here rather than as page content so the contact page, the
    /// footer and the storefront's structured data all read one source — the site previously
    /// said Chennai on the contact page and Madurai in its structured data (055).
    /// </summary>
    string ContactAddress, string ContactMobile1, string ContactMobile2,
    string ContactLandline1, string ContactLandline2, string ContactEmail, string ContactHours, string ContactCity,
    /// <summary>Footer social icons. Each is shown only when its Enabled flag is true and Url is set.</summary>
    string SocialFacebookUrl, bool SocialFacebookEnabled,
    string SocialInstagramUrl, bool SocialInstagramEnabled,
    string SocialXUrl, bool SocialXEnabled,
    string SocialLinkedinUrl, bool SocialLinkedinEnabled,
    /// <summary>Homepage meta description. Falls back to a built-in line when blank.</summary>
    string HomeMetaDescription);

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
                     || s.SettingKey == "Site.FooterDescription"
                     || s.SettingKey == "Site.NameSize"
                     || s.SettingKey == "QuickOrder.AnnouncementText"
                     || s.SettingKey == "QuickOrder.PriceValidUpto"
                     || s.SettingKey == "Store.AddressLine"
                     || s.SettingKey == "Store.Mobile1" || s.SettingKey == "Store.Mobile2"
                     || s.SettingKey == "Store.Landline1" || s.SettingKey == "Store.Landline2"
                     || s.SettingKey == "Store.Email" || s.SettingKey == "Store.Hours"
                     || s.SettingKey == "Store.City"
                     || s.SettingKey == "Social.FacebookUrl" || s.SettingKey == "Social.FacebookEnabled"
                     || s.SettingKey == "Social.InstagramUrl" || s.SettingKey == "Social.InstagramEnabled"
                     || s.SettingKey == "Social.XUrl" || s.SettingKey == "Social.XEnabled"
                     || s.SettingKey == "Social.LinkedinUrl" || s.SettingKey == "Social.LinkedinEnabled"
                     || s.SettingKey == "Site.HomeMetaDescription")
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? "", ct);

        string Get(string key) => rows.TryGetValue(key, out var v) ? v : "";
        bool GetBool(string key) => Get(key) == "true";

        return Ok(ApiResponse<SiteBrandingDto>.Ok(new SiteBrandingDto(
            Get("Site.BrowserTitle"),
            Get("Site.FaviconUrl"),
            Get("Site.Name"),
            Get("Site.LogoUrl"),
            Get("Site.NameAccent"),
            Get("Site.NameSize"),
            Get("Site.FooterLogoUrl"),
            Get("Site.FooterDescription"),
            Get("QuickOrder.AnnouncementText"),
            Get("QuickOrder.PriceValidUpto"),
            Get("Store.AddressLine"),
            Get("Store.Mobile1"),
            Get("Store.Mobile2"),
            Get("Store.Landline1"),
            Get("Store.Landline2"),
            Get("Store.Email"),
            Get("Store.Hours"),
            Get("Store.City"),
            Get("Social.FacebookUrl"), GetBool("Social.FacebookEnabled"),
            Get("Social.InstagramUrl"), GetBool("Social.InstagramEnabled"),
            Get("Social.XUrl"), GetBool("Social.XEnabled"),
            Get("Social.LinkedinUrl"), GetBool("Social.LinkedinEnabled"),
            Get("Site.HomeMetaDescription"))));
    }
}
