using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Domains;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

public class DomainServiceTests
{
    // The HTTP factory is only touched by VerifyAsync; connect/get/disconnect never call it.
    private sealed class ThrowingHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("not used in this test");
    }

    // Cloudflare-for-SaaS disabled: DomainService then behaves as it did before custom-hostname provisioning.
    private sealed class NoopCloudflare : ICloudflareSaas
    {
        public bool Enabled => false;
        public string? CnameTarget => null;
        public Task<CustomHostnameStatus?> EnsureAsync(string hostname, CancellationToken ct = default) => Task.FromResult<CustomHostnameStatus?>(null);
        public Task<CustomHostnameStatus?> GetAsync(string hostname, CancellationToken ct = default) => Task.FromResult<CustomHostnameStatus?>(null);
        public Task DeleteAsync(string hostname, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static DomainService NewSvc(ecomm.api.Data.Context.EcommerceDbContext db, long tenantId = 1, string baseDomain = "wavcommerce.online")
        => new(db, new FixedTenant(tenantId), Options.Create(new ecomm.api.Common.Tenancy.TenancyOptions { BaseDomain = baseDomain }),
            new ThrowingHttpFactory(), new MemoryCache(new MemoryCacheOptions()), new NoopCloudflare(), NullLogger<DomainService>.Instance);

    private static async Task<ecomm.api.Data.Context.EcommerceDbContext> DbWithTenant(long id = 1)
    {
        var db = TestDb.New(id);
        db.Tenants.Add(new Tenant { TenantId = id, Name = "A", Slug = "a", IsActive = true });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Connect_stores_domain_unverified_with_token()
    {
        using var db = await DbWithTenant();
        var svc = NewSvc(db);

        var s = await svc.ConnectAsync("  HTTPS://Shop.YourBrand.com/  ");   // normalized

        Assert.Equal("shop.yourbrand.com", s.Domain);
        Assert.False(s.Verified);
        Assert.False(string.IsNullOrEmpty(s.VerificationToken));
        Assert.Equal("wavcommerce.online", s.CnameTarget);
    }

    [Fact]
    public async Task Connect_rejects_invalid_domain_and_platform_subdomain()
    {
        using var db = await DbWithTenant();
        var svc = NewSvc(db);

        await Assert.ThrowsAsync<AppException>(() => svc.ConnectAsync("not a domain"));
        await Assert.ThrowsAsync<AppException>(() => svc.ConnectAsync("mystore.wavcommerce.online"));   // platform host
    }

    [Fact]
    public async Task Connect_rejects_domain_taken_by_another_tenant()
    {
        using var db = await DbWithTenant(1);
        db.Tenants.Add(new Tenant { TenantId = 2, Name = "B", Slug = "b", IsActive = true, CustomDomain = "taken.com" });
        await db.SaveChangesAsync();
        var svc = NewSvc(db, tenantId: 1);

        await Assert.ThrowsAsync<AppException>(() => svc.ConnectAsync("taken.com"));
    }

    [Fact]
    public async Task Disconnect_clears_domain_state()
    {
        using var db = await DbWithTenant();
        var svc = NewSvc(db);
        await svc.ConnectAsync("shop.brand.com");

        var s = await svc.DisconnectAsync();

        Assert.Null(s.Domain);
        Assert.False(s.Verified);
        Assert.Null(s.VerificationToken);
    }
}
