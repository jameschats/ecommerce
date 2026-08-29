using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Common.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Apps;

/// <summary>OAuth 2.0 app-install flow. Consent/approve run in the merchant admin (JWT); token exchange is
/// server-to-server from the app (anonymous, authenticated by client id + secret).</summary>
[ApiController]
[Route("api/oauth")]
public sealed class OAuthController(IAppService apps, ICurrentTenantService tenant) : ControllerBase
{
    private long? UserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>What the merchant is about to grant — drives the consent screen.</summary>
    [HttpGet("consent")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Consent([FromQuery] string clientId, [FromQuery] string? scope, [FromQuery] string redirectUri, CancellationToken ct)
        => Ok(ApiResponse<AppConsentDto>.Ok(await apps.GetConsentAsync(clientId, scope, redirectUri, ct)));

    /// <summary>Merchant approves the install → issue an authorization code and return the redirect URL.</summary>
    [HttpPost("authorize")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Authorize([FromBody] AuthorizeRequest req, CancellationToken ct)
    {
        var result = await apps.ApproveAsync(req.ClientId, req.Scope, req.RedirectUri, req.State, tenant.CurrentTenantId, UserId, ct);
        return Ok(ApiResponse<AuthorizeResult>.Ok(result));
    }

    /// <summary>Exchange the authorization code for a per-(app,tenant) access token. Called by the app server.</summary>
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> Token([FromBody] TokenRequest req, CancellationToken ct)
        => Ok(ApiResponse<AppTokenResult>.Ok(await apps.ExchangeCodeAsync(req.ClientId, req.ClientSecret, req.Code, req.RedirectUri, ct)));
}

public sealed record AuthorizeRequest(string ClientId, string? Scope, string RedirectUri, string? State);
public sealed record TokenRequest(string ClientId, string ClientSecret, string Code, string RedirectUri);

/// <summary>Merchant App Store — browse listed apps + manage installed ones.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/apps")]
public sealed class AppStoreController(IAppService apps) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Store(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<AppListingDto>>.Ok(await apps.ListStoreAsync(ct)));

    [HttpGet("installed")]
    public async Task<IActionResult> Installed(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<InstalledAppDto>>.Ok(await apps.ListInstalledAsync(ct)));

    [HttpDelete("installed/{id:long}")]
    public async Task<IActionResult> Uninstall(long id, CancellationToken ct)
    {
        await apps.UninstallAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "App uninstalled."));
    }

    private long? UserId => long.TryParse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>One-click install of a first-party app for this store.</summary>
    [HttpPost("{slug}/install")]
    public async Task<IActionResult> Install(string slug, CancellationToken ct)
        => Ok(ApiResponse<InstalledAppDto>.Ok(await apps.InstallFirstPartyAsync(slug, UserId, ct), "App installed."));

    /// <summary>An app's per-store config (installation + its key/value settings).</summary>
    [HttpGet("{slug}/config")]
    public async Task<IActionResult> GetConfig(string slug, CancellationToken ct)
    {
        var inst = await apps.GetInstalledBySlugAsync(slug, ct);
        if (inst is null) return Ok(ApiResponse<AppConfigDto>.Ok(new AppConfigDto(false, null, new Dictionary<string, string?>())));
        var settings = await apps.GetSettingsAsync(inst.InstallationId, ct);
        return Ok(ApiResponse<AppConfigDto>.Ok(new AppConfigDto(true, inst.InstallationId, settings)));
    }

    [HttpPut("{slug}/config")]
    public async Task<IActionResult> SaveConfig(string slug, [FromBody] Dictionary<string, string?> settings, CancellationToken ct)
    {
        var inst = await apps.GetInstalledBySlugAsync(slug, ct)
                   ?? throw new ecomm.api.Common.Exceptions.AppException("Install the app first.", StatusCodes.Status400BadRequest);
        await apps.SaveSettingsAsync(inst.InstallationId, settings, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Saved."));
    }
}

public sealed record AppConfigDto(bool Installed, long? InstallationId, IReadOnlyDictionary<string, string?> Settings);

/// <summary>First-party "Low Stock Alerts" app — run the check on demand (a "check now / send test" action).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/apps/low-stock-alerts")]
public sealed class LowStockAlertController(ecomm.api.Features.Apps.FirstParty.ILowStockAlertService svc) : ControllerBase
{
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        var count = await svc.RunForCurrentTenantAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { lowStockCount = count },
            count > 0 ? $"Found {count} low-stock product(s) — an alert email was sent." : "No products are low right now."));
    }
}

/// <summary>First-party "Sales Digest" app — send the digest now (a "send test" action).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/apps/sales-digest")]
public sealed class SalesDigestController(ecomm.api.Features.Apps.FirstParty.ISalesDigestService svc) : ControllerBase
{
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        var count = await svc.RunForCurrentTenantAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { orderCount = count }, "Digest email sent."));
    }
}
