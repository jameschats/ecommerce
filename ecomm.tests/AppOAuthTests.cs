using ecomm.api.Common.Exceptions;
using ecomm.api.Features.Apps;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>App-marketplace S1 — the OAuth install flow: register → consent → approve → token exchange,
/// and the token resolves to an installation with the granted scopes. Codes are single-use.</summary>
public class AppOAuthTests
{
    private const string Redirect = "https://app.test/callback";

    private static (AppService svc, FixedTenant tenant, ecomm.api.Data.Context.EcommerceDbContext db) New()
    {
        var tenant = new FixedTenant(1);
        var db = TestDb.ForDatabase(System.Guid.NewGuid().ToString(), tenant);
        return (new AppService(db, tenant), tenant, db);
    }

    private static async Task<RegisteredAppDto> RegisterAsync(AppService svc) =>
        await svc.RegisterFirstPartyAppAsync(
            new RegisterAppRequest("Test App", "desc", null, "Utilities", [Redirect], ["products:read", "orders:read"], false, null, "free"), 5);

    private static string CodeFrom(string redirectUrl)
    {
        var q = new System.Uri(redirectUrl).Query.TrimStart('?');
        var code = q.Split('&').First(p => p.StartsWith("code=")).Substring("code=".Length);
        return code;
    }

    [Fact]
    public async Task Full_install_flow_issues_a_scoped_token()
    {
        var (svc, _, db) = New();
        var app = await RegisterAsync(svc);
        Assert.StartsWith("wcapp_", app.ClientId);
        Assert.StartsWith("wcsec_", app.ClientSecret);

        var consent = await svc.GetConsentAsync(app.ClientId, null, Redirect);
        Assert.Equal(2, consent.Scopes.Count);

        var approve = await svc.ApproveAsync(app.ClientId, null, Redirect, "st8", tenantId: 1, userId: 5);
        var code = CodeFrom(approve.RedirectUrl);

        var token = await svc.ExchangeCodeAsync(app.ClientId, app.ClientSecret, code, Redirect);
        Assert.StartsWith("apptok_", token.AccessToken);
        Assert.Equal(2, token.Scopes.Count);

        var inst = await db.AppInstallations.SingleAsync();
        Assert.Equal("installed", inst.Status);
        Assert.Equal(1, inst.TenantId);
        Assert.Equal(ApiKeyHash(token.AccessToken), inst.AccessTokenHash);
    }

    [Fact]
    public async Task Authorization_code_is_single_use()
    {
        var (svc, _, _) = New();
        var app = await RegisterAsync(svc);
        var approve = await svc.ApproveAsync(app.ClientId, null, Redirect, null, 1, 5);
        var code = CodeFrom(approve.RedirectUrl);

        await svc.ExchangeCodeAsync(app.ClientId, app.ClientSecret, code, Redirect);   // first use ok
        await Assert.ThrowsAsync<AppException>(() => svc.ExchangeCodeAsync(app.ClientId, app.ClientSecret, code, Redirect));
    }

    [Fact]
    public async Task Wrong_secret_is_rejected()
    {
        var (svc, _, _) = New();
        var app = await RegisterAsync(svc);
        var approve = await svc.ApproveAsync(app.ClientId, null, Redirect, null, 1, 5);
        var code = CodeFrom(approve.RedirectUrl);

        await Assert.ThrowsAsync<AppException>(() => svc.ExchangeCodeAsync(app.ClientId, "wcsec_wrong", code, Redirect));
    }

    [Fact]
    public async Task Unregistered_redirect_uri_is_rejected()
    {
        var (svc, _, _) = New();
        var app = await RegisterAsync(svc);
        await Assert.ThrowsAsync<AppException>(() => svc.GetConsentAsync(app.ClientId, null, "https://evil.test/cb"));
    }

    private static string ApiKeyHash(string raw) => ecomm.api.Features.PublicApi.ApiKeyService.Hash(raw);
}
