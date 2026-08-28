using System.Text.RegularExpressions;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Domains;

public sealed record DomainStatusDto(
    string? Domain, bool Verified, string? VerificationPath, string? VerificationToken, string? CnameTarget,
    string? SslStatus);
public sealed record ConnectDomainRequest(string Domain);

public interface IDomainService
{
    Task<DomainStatusDto> GetAsync(CancellationToken ct = default);
    Task<DomainStatusDto> ConnectAsync(string domain, CancellationToken ct = default);
    Task<DomainStatusDto> VerifyAsync(CancellationToken ct = default);
    Task<DomainStatusDto> DisconnectAsync(CancellationToken ct = default);
}

/// <summary>
/// Custom-domain connect for the current tenant. The merchant points their domain at the platform
/// (CNAME to <c>BaseDomain</c>), then we verify control by fetching a well-known path over that host
/// and matching a per-tenant token. Verification proves the domain routes to us; TLS certificate
/// provisioning for the domain is an infrastructure step (Cloudflare / on-demand TLS) outside the app.
/// </summary>
public sealed class DomainService(
    EcommerceDbContext db, ICurrentTenantService tenant, IOptions<TenancyOptions> tenancy,
    IHttpClientFactory httpFactory, IMemoryCache cache, ICloudflareSaas cf, ILogger<DomainService> log) : IDomainService
{
    public const string WellKnownPath = "/.well-known/wavcommerce-domain-verification";
    private static readonly Regex DomainRx = new(@"^(?!-)[a-z0-9-]{1,63}(?<!-)(\.(?!-)[a-z0-9-]{1,63}(?<!-))+$", RegexOptions.Compiled);

    public async Task<DomainStatusDto> GetAsync(CancellationToken ct = default) => await ToDtoAsync(await CurrentAsync(ct), ct);

    public async Task<DomainStatusDto> ConnectAsync(string domain, CancellationToken ct = default)
    {
        var host = Normalize(domain);
        if (!DomainRx.IsMatch(host)) throw new AppException("Enter a valid domain, e.g. shop.yourbrand.com.");
        var baseDomain = tenancy.Value.BaseDomain?.ToLowerInvariant();
        if (!string.IsNullOrEmpty(baseDomain) && (host == baseDomain || host.EndsWith($".{baseDomain}", StringComparison.Ordinal)))
            throw new AppException("Use a domain you own — platform subdomains are assigned automatically.");

        // Unique across tenants (a domain can only belong to one store).
        var takenBy = await db.Tenants.AsNoTracking()
            .Where(t => t.CustomDomain == host && t.TenantId != tenant.CurrentTenantId).Select(t => t.TenantId).FirstOrDefaultAsync(ct);
        if (takenBy != 0) throw new AppException("That domain is already connected to another store.");

        var t = await CurrentAsync(ct);
        var previous = t.CustomDomain;
        t.CustomDomain = host;
        t.CustomDomainVerified = false;
        t.CustomDomainToken ??= Guid.NewGuid().ToString("N");
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        if (!string.IsNullOrEmpty(previous)) { cache.Remove($"tenant:domain:{previous}"); await cf.DeleteAsync(previous, ct); }
        cache.Remove($"tenant:domain:{host}");
        await cf.EnsureAsync(host, ct);   // start Cloudflare cert provisioning (no-op if CF unconfigured)
        return await ToDtoAsync(t, ct);
    }

    public async Task<DomainStatusDto> VerifyAsync(CancellationToken ct = default)
    {
        var t = await CurrentAsync(ct);
        if (string.IsNullOrEmpty(t.CustomDomain)) throw new AppException("Connect a domain first.");

        if (cf.Enabled)
        {
            // Cloudflare-for-SaaS path: Cloudflare validates control + issues the edge cert once the
            // merchant's CNAME points at our fallback origin. "Verified" = the edge cert is active.
            var status = await cf.EnsureAsync(t.CustomDomain, ct)
                         ?? throw new AppException("Couldn't reach Cloudflare to check the certificate. Try again in a moment.");
            if (!string.Equals(status.SslStatus, "active", StringComparison.OrdinalIgnoreCase))
                throw new AppException($"Certificate is still provisioning (status: {status.SslStatus}). Make sure the CNAME points to {cf.CnameTarget}, then retry in a few minutes.");
            t.CustomDomainVerified = true;
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            cache.Remove($"tenant:domain:{t.CustomDomain}");
            return await ToDtoAsync(t, ct);
        }

        // Fallback (Cloudflare unconfigured): prove routing via the well-known token over HTTP.
        var url = $"http://{t.CustomDomain}{WellKnownPath}";
        try
        {
            var http = httpFactory.CreateClient("domain-verify");
            var body = (await http.GetStringAsync(url, ct)).Trim();
            if (body == t.CustomDomainToken)
            {
                t.CustomDomainVerified = true;
                t.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                cache.Remove($"tenant:domain:{t.CustomDomain}");
            }
            else
            {
                throw new AppException("Domain is reachable but the verification token didn't match yet. Give DNS a few minutes and retry.");
            }
        }
        catch (AppException) { throw; }
        catch (Exception ex)
        {
            log.LogInformation(ex, "Domain verification fetch failed for {Domain}", t.CustomDomain);
            throw new AppException("Couldn't reach your domain yet. Make sure the DNS record points to us, then retry.");
        }
        return await ToDtoAsync(t, ct);
    }

    public async Task<DomainStatusDto> DisconnectAsync(CancellationToken ct = default)
    {
        var t = await CurrentAsync(ct);
        var previous = t.CustomDomain;
        t.CustomDomain = null;
        t.CustomDomainVerified = false;
        t.CustomDomainToken = null;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        if (!string.IsNullOrEmpty(previous)) { cache.Remove($"tenant:domain:{previous}"); await cf.DeleteAsync(previous, ct); }
        return await ToDtoAsync(t, ct);
    }

    private async Task<ecomm.api.Data.Entities.Tenant> CurrentAsync(CancellationToken ct) =>
        await db.Tenants.FirstOrDefaultAsync(x => x.TenantId == tenant.CurrentTenantId, ct)
            ?? throw new AppException("Store not found.", 404);

    private async Task<DomainStatusDto> ToDtoAsync(ecomm.api.Data.Entities.Tenant t, CancellationToken ct)
    {
        string? ssl = null;
        if (!string.IsNullOrEmpty(t.CustomDomain) && cf.Enabled)
            ssl = (await cf.GetAsync(t.CustomDomain, ct))?.SslStatus;
        // Merchants CNAME to the Cloudflare-for-SaaS fallback origin; without CF, to the platform apex.
        var cname = cf.CnameTarget ?? (string.IsNullOrEmpty(tenancy.Value.BaseDomain) ? null : tenancy.Value.BaseDomain);
        return new DomainStatusDto(
            t.CustomDomain, t.CustomDomainVerified,
            string.IsNullOrEmpty(t.CustomDomain) ? null : WellKnownPath,
            t.CustomDomainToken, cname, ssl);
    }

    private static string Normalize(string domain)
    {
        var d = (domain ?? "").Trim().ToLowerInvariant();
        d = Regex.Replace(d, "^https?://", "");
        return d.TrimEnd('/').Split('/')[0];
    }
}
