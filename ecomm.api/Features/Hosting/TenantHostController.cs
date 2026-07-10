using ecomm.api.Common.Models;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Hosting;

public sealed record HostInfoDto(
    string? Slug, string PlatformHost, bool IsCustomDomain, string HostType, string AdminUrl, string SuperAdminUrl);

/// <summary>
/// Public: tells the storefront app which host it's on so it can keep the merchant/super-admin
/// consoles OFF a merchant's custom domain. On a connected custom domain the app redirects
/// <c>/admin</c> → the platform admin and <c>/superadmin</c> → the apex super-admin.
/// </summary>
[ApiController]
[Route("api/tenant")]
public sealed class TenantHostController(
    EcommerceDbContext db, ICurrentTenantService tenant, IOptions<TenancyOptions> tenancy) : ControllerBase
{
    [HttpGet("host-info")]
    public async Task<IActionResult> HostInfo(CancellationToken ct)
    {
        var t = await db.Tenants.AsNoTracking()
            .Where(x => x.TenantId == tenant.CurrentTenantId)
            .Select(x => new { x.Slug, x.CustomDomain })
            .FirstOrDefaultAsync(ct);

        var baseDomain = (tenancy.Value.BaseDomain ?? "").ToLowerInvariant();
        var host = RequestHost();

        // The store's stable platform address: "{slug}.{baseDomain}" (falls back to the current host in dev).
        var platformHost = !string.IsNullOrEmpty(baseDomain) && !string.IsNullOrEmpty(t?.Slug)
            ? $"{t!.Slug}.{baseDomain}"
            : host;

        // We're on a custom domain when the request host is the tenant's connected domain (not its subdomain/apex).
        var isCustomDomain = !string.IsNullOrEmpty(t?.CustomDomain)
            && string.Equals(host, t!.CustomDomain, StringComparison.OrdinalIgnoreCase);

        var scheme = string.IsNullOrEmpty(baseDomain) ? "http" : "https";
        var apex = string.IsNullOrEmpty(baseDomain) ? host : baseDomain;

        // apex = the platform root (wavcommerce.online / www / dev localhost); custom = a connected brand
        // domain; store = a "{slug}.{baseDomain}" subdomain. Drives landing-vs-storefront + chrome.
        var isApex = string.IsNullOrEmpty(baseDomain)
            ? host is "localhost" or "127.0.0.1" or ""
            : host == baseDomain || host == $"www.{baseDomain}";
        var hostType = isCustomDomain ? "custom" : isApex ? "apex" : "store";

        return Ok(ApiResponse<HostInfoDto>.Ok(new HostInfoDto(
            t?.Slug, platformHost, isCustomDomain, hostType,
            AdminUrl: $"{scheme}://{platformHost}/admin",
            SuperAdminUrl: $"{scheme}://{apex}/superadmin")));
    }

    /// <summary>The public host of this request (X-Forwarded-Host wins behind the SSR/Nginx proxy).</summary>
    private string RequestHost()
    {
        var fwd = Request.Headers["X-Forwarded-Host"].ToString();
        var host = (!string.IsNullOrEmpty(fwd) ? fwd.Split(',')[0].Trim() : Request.Host.Host).ToLowerInvariant();
        var portIdx = host.IndexOf(':');
        return portIdx >= 0 ? host[..portIdx] : host;
    }
}
