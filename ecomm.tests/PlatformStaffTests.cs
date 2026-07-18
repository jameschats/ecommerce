using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.SuperAdmin;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class PlatformStaffTests
{
    private static PlatformStaffService New(EcommerceDbContext db) => new(db, new BcryptPasswordHasher());

    private static void SeedRole(EcommerceDbContext db)
    {
        db.Roles.Add(new Role { RoleId = 1, TenantId = 1, Name = "SuperAdmin", NormalizedName = "SUPERADMIN" });
        db.SaveChanges();
    }

    [Fact]
    public async Task Create_adds_a_superadmin_and_list_shows_them()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedRole(db);
        var svc = New(db);

        var created = await svc.CreateAsync(new CreateStaffRequest("ops@x.com", "Ops", "password123"), actingUserId: 99, default);
        Assert.True(created.UserId > 0);

        var list = await svc.ListAsync(default);
        Assert.Contains(list, s => s.Email == "ops@x.com");
        Assert.True(await db.UserRoles.AnyAsync(ur => ur.UserId == created.UserId && ur.RoleId == 1));
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedRole(db);
        var svc = New(db);
        await svc.CreateAsync(new CreateStaffRequest("dup@x.com", null, "password123"), 1, default);

        await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(new CreateStaffRequest("dup@x.com", null, "password123"), 1, default));
    }

    [Fact]
    public async Task Cannot_revoke_own_access()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedRole(db);
        var svc = New(db);
        var me = await svc.CreateAsync(new CreateStaffRequest("me@x.com", "Me", "password123"), 1, default);

        await Assert.ThrowsAsync<AppException>(() => svc.SetActiveAsync(me.UserId, false, actingUserId: me.UserId, default));
    }
}
