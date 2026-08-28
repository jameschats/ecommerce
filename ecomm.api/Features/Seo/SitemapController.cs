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
    private long Tenant => _db.CurrentTenantId;
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
        // Per-tenant: each store's sitemap must list ITS OWN host (bazaar.wavcommerce.online), not a
        // single platform-wide URL — otherwise every tenant's sitemap points search engines at the same
        // domain. Derive it from the request host (X-Forwarded-Host wins behind the SSR/Nginx proxy),
        // falling back to config only when there's no host (dev).
        var baseUrl = RequestBaseUrl() ?? (_config["Cors:AngularOrigin"] ?? "https://wavcommerce.online").TrimEnd('/');

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

    /// <summary>This request's public origin (scheme+host), X-Forwarded-* winning behind the proxy; null in dev with no host.</summary>
    private string? RequestBaseUrl()
    {
        var fwdHost = Request.Headers["X-Forwarded-Host"].ToString();
        var host = (!string.IsNullOrEmpty(fwdHost) ? fwdHost.Split(',')[0].Trim() : Request.Host.Value ?? "").Trim();
        if (string.IsNullOrEmpty(host)) return null;
        var fwdProto = Request.Headers["X-Forwarded-Proto"].ToString();
        var scheme = !string.IsNullOrEmpty(fwdProto) ? fwdProto.Split(',')[0].Trim() : Request.Scheme;
        return $"{scheme}://{host}".TrimEnd('/');
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
