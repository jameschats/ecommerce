using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.Staff;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class StaffAdminTests
{
    private static async Task<(EcommerceDbContext db, StaffAdminService svc, long ownerId)> SetupAsync()
    {
        var db = TestDb.New(tenantId: 1);
        db.Roles.Add(new Role { RoleId = 1, TenantId = 1, Name = "Admin", NormalizedName = "ADMIN" });
        var owner = new User { UserId = 1, Email = "owner@x.com", NormalizedEmail = "OWNER@X.COM", FullName = "Owner", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        db.TenantStaff.Add(new TenantStaff { UserId = owner.UserId, AccessLevel = StaffAccess.Owner, Status = StaffStatus.Active, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return (db, new StaffAdminService(db, new BcryptPasswordHasher()), owner.UserId);
    }

    [Fact]
    public async Task Owner_invites_staff_with_admin_role_and_level()
    {
        var (db, svc, ownerId) = await SetupAsync();
        using var _ = db;

        var m = await svc.InviteAsync(ownerId, new InviteStaffRequest("New Staff", "staff@x.com", "Staff", "password123"));

        Assert.Equal(StaffAccess.Staff, m.AccessLevel);
        Assert.True(await db.UserRoles.AnyAsync(ur => ur.UserId == m.UserId && ur.RoleId == 1));   // has ADMIN role
        Assert.Equal(2, (await svc.ListAsync(ownerId)).Count);
    }

    [Fact]
    public async Task Staff_level_member_cannot_manage_staff()
    {
        var (db, svc, ownerId) = await SetupAsync();
        using var _ = db;

        var staff = await svc.InviteAsync(ownerId, new InviteStaffRequest("Sam", "sam@x.com", "Staff", "password123"));

        await Assert.ThrowsAsync<AppException>(
            () => svc.InviteAsync(staff.UserId, new InviteStaffRequest("X", "x@x.com", "Staff", "password123")));
    }

    [Fact]
    public async Task Owner_cannot_be_removed_or_disabled()
    {
        var (db, svc, ownerId) = await SetupAsync();
        using var _ = db;

        var admin = await svc.InviteAsync(ownerId, new InviteStaffRequest("Ada", "ada@x.com", "Admin", "password123"));

        await Assert.ThrowsAsync<AppException>(() => svc.RemoveAsync(admin.UserId, ownerId));
        await Assert.ThrowsAsync<AppException>(() => svc.SetStatusAsync(admin.UserId, ownerId, StaffStatus.Disabled));
    }
}
