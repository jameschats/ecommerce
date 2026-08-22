using System.Security.Claims;
using System.Text.Encodings.Web;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.PublicApi;

public static class ApiKeyAuthDefaults
{
    public const string Scheme = "ApiKey";
}

/// <summary>
/// Authenticates the public API (v4 Phase 6 Track A) with a per-tenant key, deliberately registered
/// as a SECOND scheme alongside JWT Bearer rather than folded into it — public controllers declare
/// <c>[Authorize(AuthenticationSchemes = ApiKeyAuthDefaults.Scheme)]</c> explicitly, so a bare
/// <c>[Authorize]</c> elsewhere in the app keeps meaning "JWT Bearer" exactly as it always has.
///
/// Stamps a <c>"tenant"</c> claim from the key's own <c>TenantId</c> — <see cref="Common.Tenancy.TenantResolutionMiddleware"/>
/// already cross-checks any authenticated principal's <c>"tenant"</c> claim against the Host-resolved
/// tenant for free, so a key issued for one store can't be used against another's subdomain without
/// writing that check twice.
///
/// Runs before <c>TenantResolutionMiddleware</c> in the pipeline (auth executes first), so the key
/// lookup can't rely on <c>db.CurrentTenantId</c> being set yet — <c>IgnoreQueryFilters()</c> is
/// required here, not a workaround.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, EcommerceDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var rawKey = ExtractKey(Request);
        if (rawKey is null) return AuthenticateResult.NoResult();
        if (!rawKey.StartsWith(ApiKeyService.KeyPrefixLiteral, StringComparison.Ordinal))
            return AuthenticateResult.Fail("Invalid API key format.");

        var hash = ApiKeyService.Hash(rawKey);
        var key = await db.ApiKeys.IgnoreQueryFilters().FirstOrDefaultAsync(k => k.KeyHash == hash);
        if (key is null || key.RevokedAt is not null)
            return AuthenticateResult.Fail("Invalid or revoked API key.");

        key.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new("tenant", key.TenantId.ToString()),
            new("apikey_id", key.ApiKeyId.ToString()),
        };
        claims.AddRange(key.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => new Claim("scope", s)));

        var identity = new ClaimsIdentity(claims, ApiKeyAuthDefaults.Scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyAuthDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }

    private static string? ExtractKey(HttpRequest req)
    {
        var auth = req.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return auth["Bearer ".Length..].Trim();
        if (req.Headers.TryGetValue("X-Api-Key", out var xk) && !string.IsNullOrWhiteSpace(xk)) return xk.ToString().Trim();
        return null;
    }
}

/// <summary>Requires the authenticated API key to carry a specific scope — same
/// <see cref="Microsoft.AspNetCore.Mvc.Filters.IAsyncActionFilter"/> pattern as
/// <see cref="ecomm.api.Features.Plans.RequiresFeatureAttribute"/>.</summary>
public sealed class RequiresScopeAttribute(string scope) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var hasScope = context.HttpContext.User.FindAll("scope").Any(c => c.Value.Equals(scope, StringComparison.OrdinalIgnoreCase));
        if (!hasScope)
            throw new AppException($"This API key doesn't have the '{scope}' scope.", StatusCodes.Status403Forbidden);
        await next();
    }
}
