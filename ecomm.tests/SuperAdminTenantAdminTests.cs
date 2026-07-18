using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.SuperAdmin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

public class SuperAdminTenantAdminTests
{
    // The lifecycle methods under test never mint a token → a throwing stub is enough.
    private sealed class StubJwt : IJwtTokenService
    {
        public (string, DateTime) CreateAccessToken(User u, IEnumerable<string> roles, IEnumerable<string> perms) => throw new NotImplementedException();
        public (string, DateTime) CreateImpersonationToken(User u, IEnumerable<string> roles, string mode, long by, int minutes = 30) => throw new NotImplementedException();
        public (string, string, DateTime) CreateRefreshToken() => throw new NotImplementedException();
        public string HashRefreshToken(string raw) => throw new NotImplementedException();
    }

    private static SuperAdminService New(EcommerceDbContext db) =>
        new(db, new StubJwt(), Options.Create(new TenancyOptions { BaseDomain = "wavcommerce.online" }));

    private static void SeedTenant(EcommerceDbContext db, int? planId = null)
        => db.Tenants.Add(new Tenant { TenantId = 2, Name = "Acme", Code = "acme", IsActive = true, CreatedAt = DateTime.UtcNow, PlanId = planId });

    [Fact]
    public async Task ChangePlan_updates_tenant_and_subscription()
    {
        using var db = TestDb.New(tenantId: 2);
        SeedTenant(db, planId: 1);
        db.Plans.Add(new Plan { PlanId = 1, Name = "Basic", Slug = "basic", MonthlyPrice = 0 });
        db.Plans.Add(new Plan { PlanId = 2, Name = "Pro", Slug = "pro", MonthlyPrice = 999 });
        db.TenantSubscriptions.Add(new TenantSubscription { TenantId = 2, PlanId = 1, Status = "Trial", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await New(db).ChangePlanAsync(2, 2, adminUserId: 1, default);

        Assert.Equal(2, (await db.Tenants.FirstAsync(x => x.TenantId == 2)).PlanId);
        Assert.Equal(2, (await db.TenantSubscriptions.IgnoreQueryFilters().FirstAsync(x => x.TenantId == 2)).PlanId);
    }

    [Fact]
    public async Task Offboard_then_reactivate_toggles_state()
    {
        using var db = TestDb.New(tenantId: 2);
        SeedTenant(db);
        await db.SaveChangesAsync();
        var svc = New(db);

        await svc.OffboardAsync(2, 1, default);
        var t = await db.Tenants.FirstAsync(x => x.TenantId == 2);
        Assert.False(t.IsActive);
        Assert.NotNull(t.OffboardedAt);

        await svc.SetActiveAsync(2, true, 1, default);
        t = await db.Tenants.FirstAsync(x => x.TenantId == 2);
        Assert.True(t.IsActive);
        Assert.Null(t.OffboardedAt);   // reactivate clears off-board
    }

    [Fact]
    public async Task Notes_append_and_tags_normalize()
    {
        using var db = TestDb.New(tenantId: 2);
        SeedTenant(db);
        await db.SaveChangesAsync();
        var svc = New(db);

        await svc.AddNoteAsync(2, "  called merchant  ", 1, default);
        await svc.SetTagsAsync(2, "vip, wholesale, vip", 1, default);

        Assert.Equal("called merchant", (await db.TenantNotes.FirstAsync()).Note);
        Assert.Equal("vip,wholesale", (await db.Tenants.FirstAsync(x => x.TenantId == 2)).PlatformTags);
    }
}
