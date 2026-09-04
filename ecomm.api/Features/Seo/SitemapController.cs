using System.Text;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Seo;

/// <summary>Generates the XML sitemap from live categories + products for search engines.</summary>
[ApiController]
public sealed class SitemapController : ControllerBase
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;
    private readonly IConfiguration _config;

    public SitemapController(EcommerceDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// The public address of the shop, which every &lt;loc&gt; in the sitemap is built from.
    ///
    /// This used to read Cors:AngularOrigin, which production never sets — so it fell through to
    /// the localhost value in appsettings.json and the live sitemap advertised every page as
    /// http://localhost:4200/... to search engines. It now reads the Site.Url setting, the same
    /// one OrderMailer uses for links in customer email, so the shop has one answer to "where do
    /// we live" rather than two that can disagree.
    /// </summary>
    private async Task<string> BaseUrlAsync(CancellationToken ct)
    {
        var stored = await _db.Settings
            .Where(s => s.TenantId == Tenant && s.SettingKey == "Site.Url")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(ct);

        var url = !string.IsNullOrWhiteSpace(stored) ? stored : _config["Site:Url"];

        // No guessed default. A sitemap pointing at the wrong shop is worse than no sitemap, and
        // a wrong one is silent — this at least fails where somebody will see it.
        if (string.IsNullOrWhiteSpace(url))
            throw new Common.Exceptions.AppException(
                "Site.Url is not set, so the sitemap cannot say where the shop lives. "
                + "Set it in Settings before submitting a sitemap.");

        return url.TrimEnd('/');
    }

    [OutputCache(PolicyName = "public")]
    [HttpGet("api/sitemap.xml")]
    [Produces("application/xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var baseUrl = await BaseUrlAsync(ct);

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        void Url(string path, DateTime? lastMod = null, string freq = "weekly")
        {
            sb.Append("<url><loc>").Append(baseUrl).Append(Escape(path)).Append("</loc>");
            if (lastMod is { } lm) sb.Append("<lastmod>").Append(lm.ToString("yyyy-MM-dd")).Append("</lastmod>");
            sb.Append("<changefreq>").Append(freq).Append("</changefreq></url>");
        }

        // Static / landing pages
        Url("/", freq: "daily");
        Url("/products", freq: "daily");
        Url("/about", freq: "monthly");
        Url("/contact", freq: "monthly");
        Url("/faq", freq: "monthly");

        // Categories
        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.TenantId == Tenant && c.IsActive)
            .Select(c => c.Slug).ToListAsync(ct);
        foreach (var slug in categories) Url($"/category/{slug}");

        // Live products (same visibility rule as the storefront)
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.TenantId == Tenant && !p.IsDeleted && p.IsActive && p.Status == "Active")
            .Select(p => new { p.Slug, p.UpdatedAt, p.CreatedAt }).ToListAsync(ct);
        foreach (var p in products) Url($"/product/{p.Slug}", p.UpdatedAt ?? p.CreatedAt);

        sb.Append("</urlset>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
