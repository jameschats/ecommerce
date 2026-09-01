using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Features.MarketingStudio;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

public class SocialConnectionServiceTests
{
    private sealed class StubHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private static SocialConnectionService New(EcommerceDbContext db, SocialOptions? opt = null) =>
        new(db, new FixedTenant(1), new EphemeralDataProtectionProvider(),
            new StubHttpFactory(), Options.Create(opt ?? new SocialOptions()));

    private static SocialOptions Configured() => new()
    {
        RedirectBaseUrl = "https://app.wavcommerce.online",
        Providers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["linkedin"] = new SocialProviderConfig { ClientId = "li-client", ClientSecret = "li-secret" },
        },
    };

    [Fact]
    public async Task List_returns_a_card_per_platform_all_not_configured_by_default()
    {
        using var db = TestDb.New(tenantId: 1);
        var list = await New(db).ListAsync();

        Assert.Equal(SocialPlatforms.All.Count, list.Count);
        Assert.All(list, c => Assert.False(c.Configured));
        Assert.All(list, c => Assert.Equal("not_configured", c.Status));
        Assert.Contains(list, c => c.Platform == "linkedin" && c.DisplayName == "LinkedIn");
    }

    [Fact]
    public async Task List_marks_a_configured_platform_not_connected()
    {
        using var db = TestDb.New(tenantId: 1);
        var list = await New(db, Configured()).ListAsync();

        var li = Assert.Single(list, c => c.Platform == "linkedin");
        Assert.True(li.Configured);
        Assert.Equal("not_connected", li.Status);
    }

    [Fact]
    public async Task Start_builds_an_authorize_url_with_client_id_redirect_scope_and_state()
    {
        using var db = TestDb.New(tenantId: 1);
        var res = await New(db, Configured()).StartAsync("linkedin");

        Assert.StartsWith("https://www.linkedin.com/oauth/v2/authorization?", res.AuthorizeUrl);
        Assert.Contains("client_id=li-client", res.AuthorizeUrl);
        Assert.Contains("response_type=code", res.AuthorizeUrl);
        Assert.Contains(Uri.EscapeDataString("https://app.wavcommerce.online/api/marketing/connections/linkedin/callback"), res.AuthorizeUrl);
        Assert.Contains("state=", res.AuthorizeUrl);
        Assert.Contains("scope=", res.AuthorizeUrl);
    }

    [Fact]
    public async Task Start_on_an_unconfigured_platform_is_rejected()
    {
        using var db = TestDb.New(tenantId: 1);
        var ex = await Assert.ThrowsAsync<AppException>(() => New(db).StartAsync("pinterest"));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Start_on_an_unknown_platform_is_404()
    {
        using var db = TestDb.New(tenantId: 1);
        var ex = await Assert.ThrowsAsync<AppException>(() => New(db, Configured()).StartAsync("myspace"));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Complete_with_a_tampered_state_is_rejected()
    {
        using var db = TestDb.New(tenantId: 1);
        var ex = await Assert.ThrowsAsync<AppException>(() => New(db, Configured()).CompleteAsync("linkedin", "some-code", "not-a-valid-state"));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Disconnect_is_a_noop_when_nothing_is_connected()
    {
        using var db = TestDb.New(tenantId: 1);
        await New(db).DisconnectAsync("linkedin");   // must not throw
        Assert.Empty(db.SocialConnections);
    }
}
