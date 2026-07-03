using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ecomm.api.Common.Tenancy;

/// <summary>
/// Resolves the tenant from the request Host subdomain and stashes it on
/// HttpContext.Items before any controller runs.
///
///  • apex / www / localhost / IP / no BaseDomain  → default tenant (V1 stays working)
///  • "{slug}.{BaseDomain}"                          → look up the tenant by Slug
///  • unknown or inactive/suspended subdomain        → 404 (fail-closed)
///
/// Lookup is cached in-memory (per instance). Redis is the multi-instance target
/// once the platform scales horizontally (design-v2.md §6.2).
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, IOptions<TenancyOptions> options, ILogger<TenantResolutionMiddleware> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private readonly TenancyOptions _opt = options.Value;

    public async Task InvokeAsync(HttpContext context, EcommerceDbContext db, IMemoryCache cache)
    {
        var host = context.Request.Host.Host.ToLowerInvariant();
        var slug = ExtractSlug(host);
        long tenantId;

        if (slug is null)
        {
            // apex / www / localhost / IP / no BaseDomain → default tenant
            tenantId = _opt.DefaultTenantId;
        }
        else
        {
            var resolved = await cache.GetOrCreateAsync($"tenant:slug:{slug}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                return await db.Tenants
                    .AsNoTracking()
                    .Where(t => t.Slug == slug)
                    .Select(t => new ResolvedTenant(t.TenantId, t.IsActive, t.SuspendedAt))
                    .FirstOrDefaultAsync();
            });

            if (resolved is null || !resolved.IsActive || resolved.SuspendedAt is not null)
            {
                logger.LogWarning("Tenant not resolvable for host {Host} (slug {Slug})", host, slug);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Store not found.");
                return;
            }
            tenantId = resolved.TenantId;
        }

        // Defence in depth: a token issued for one store can't be used against another
        // (e.g. a tenant-A admin token pointed at tenant-B's subdomain).
        var tokenTenant = context.User.FindFirst("tenant")?.Value;
        if (tokenTenant is not null && long.TryParse(tokenTenant, out var tt) && tt != tenantId)
        {
            logger.LogWarning("Token tenant {TokenTenant} != host tenant {HostTenant} on {Host}", tokenTenant, tenantId, host);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("This sign-in is not valid for this store.");
            return;
        }

        context.Items[CurrentTenantService.HttpContextItemKey] = tenantId;
        await next(context);
    }

    /// <summary>Returns the subdomain slug for a "{slug}.{BaseDomain}" host, or null for apex/www/dev.</summary>
    private string? ExtractSlug(string host)
    {
        if (string.IsNullOrEmpty(_opt.BaseDomain)) return null;
        var baseDomain = _opt.BaseDomain.ToLowerInvariant();
        if (host == baseDomain || host == $"www.{baseDomain}") return null;
        var suffix = $".{baseDomain}";
        if (!host.EndsWith(suffix, StringComparison.Ordinal)) return null;   // localhost, IPs, other domains
        var slug = host[..^suffix.Length];
        return string.IsNullOrEmpty(slug) || slug == "www" ? null : slug;
    }

    private sealed record ResolvedTenant(long TenantId, bool IsActive, DateTime? SuspendedAt);
}
