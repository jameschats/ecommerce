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

    [OutputCache(PolicyName = "public")]
    [HttpGet("api/sitemap.xml")]
    [Produces("application/xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var baseUrl = (_config["Cors:AngularOrigin"] ?? "https://calendarshop.online").TrimEnd('/');

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
