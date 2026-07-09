using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Staff;

public sealed record StaffMember(
    long UserId, string? FullName, string? Email, string AccessLevel, string Status,
    DateTime? LastLoginAt, DateTime CreatedAt, bool IsYou);

public sealed record StaffRoleInfo(string Key, string Label, string Description, bool CanManageStaff);

public sealed record InviteStaffRequest(string? FullName, string? Email, string AccessLevel, string? Password);
public sealed record UpdateStaffRoleRequest(string AccessLevel);

public interface IStaffAdminService
{
    Task<IReadOnlyList<StaffMember>> ListAsync(long currentUserId, CancellationToken ct = default);
    IReadOnlyList<StaffRoleInfo> Roles();
    Task<StaffMember> InviteAsync(long actorUserId, InviteStaffRequest req, CancellationToken ct = default);
    Task<StaffMember> UpdateRoleAsync(long actorUserId, long userId, string accessLevel, CancellationToken ct = default);
    Task<StaffMember> SetStatusAsync(long actorUserId, long userId, string status, CancellationToken ct = default);
    Task RemoveAsync(long actorUserId, long userId, CancellationToken ct = default);
}

/// <summary>
/// Staff = <see cref="User"/>s in the ADMIN role with a <see cref="TenantStaff"/> access level.
/// Only Owner/Admin may manage staff (enforced here); Viewer read-only + Disabled-blocked are
/// enforced globally by StaffAccessGuardMiddleware. Tenant scoping via global query filters.
/// </summary>
public sealed class StaffAdminService(EcommerceDbContext db, IPasswordHasher hasher) : IStaffAdminService
{
    private const string AdminRole = "ADMIN";

    public async Task<IReadOnlyList<StaffMember>> ListAsync(long currentUserId, CancellationToken ct = default) =>
        await (from ts in db.TenantStaff
               join u in db.Users on ts.UserId equals u.UserId
               where !u.IsDeleted
               orderby ts.AccessLevel == StaffAccess.Owner ? 0 : 1, ts.CreatedAt
               select new StaffMember(u.UserId, u.FullName, u.Email, ts.AccessLevel, ts.Status,
                   u.LastLoginAt, ts.CreatedAt, u.UserId == currentUserId)).ToListAsync(ct);

    public IReadOnlyList<StaffRoleInfo> Roles() => new List<StaffRoleInfo>
    {
        new(StaffAccess.Owner, "Owner", "Full access, including staff and store ownership. Only one owner.", true),
        new(StaffAccess.Admin, "Admin", "Full access, including managing staff.", true),
        new(StaffAccess.Staff, "Staff", "Manage products, orders and customers. Cannot manage staff.", false),
        new(StaffAccess.Viewer, "Viewer", "Read-only access to the whole admin.", false),
    };

    public async Task<StaffMember> InviteAsync(long actorUserId, InviteStaffRequest req, CancellationToken ct = default)
    {
        await EnsureCanManageAsync(actorUserId, ct);
        var level = NormalizeLevel(req.AccessLevel);
        if (level == StaffAccess.Owner) throw new AppException("There can only be one owner. Assign Admin instead.");

        var email = req.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email)) throw new AppException("A staff member needs an email.");
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password!.Length < 8)
            throw new AppException("Set an initial password of at least 8 characters for the new staff member.");

        var normalized = email.ToUpperInvariant();
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized && !u.IsDeleted, ct))
            throw new AppException("A user with that email already exists.", StatusCodes.Status409Conflict);

        var user = new User
        {
            Email = email,
            NormalizedEmail = normalized,
            FullName = req.FullName?.Trim(),
            PasswordHash = hasher.Hash(req.Password),
            IsActive = true,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);   // TenantId auto-stamped; UserId assigned

        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == AdminRole, ct)
            ?? throw new AppException("Admin role is missing.", StatusCodes.Status500InternalServerError);
        db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = adminRole.RoleId });
        db.TenantStaff.Add(new TenantStaff
        {
            UserId = user.UserId, AccessLevel = level, Status = StaffStatus.Active,
            InvitedByUserId = actorUserId, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return (await GetAsync(user.UserId, actorUserId, ct))!;
    }

    public async Task<StaffMember> UpdateRoleAsync(long actorUserId, long userId, string accessLevel, CancellationToken ct = default)
    {
        await EnsureCanManageAsync(actorUserId, ct);
        var level = NormalizeLevel(accessLevel);
        var staff = await FindAsync(userId, ct) ?? throw NotFound();

        if (staff.AccessLevel == StaffAccess.Owner) throw new AppException("The owner's role can't be changed here.");
        if (level == StaffAccess.Owner) throw new AppException("There can only be one owner.");
        if (userId == actorUserId) throw new AppException("You can't change your own role.");

        staff.AccessLevel = level;
        staff.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(userId, actorUserId, ct))!;
    }

    public async Task<StaffMember> SetStatusAsync(long actorUserId, long userId, string status, CancellationToken ct = default)
    {
        await EnsureCanManageAsync(actorUserId, ct);
        var s = status == StaffStatus.Disabled ? StaffStatus.Disabled : StaffStatus.Active;
        var staff = await FindAsync(userId, ct) ?? throw NotFound();

        if (staff.AccessLevel == StaffAccess.Owner) throw new AppException("The owner can't be disabled.");
        if (userId == actorUserId) throw new AppException("You can't disable your own account.");

        staff.Status = s;
        staff.UpdatedAt = DateTime.UtcNow;
        var user = await db.Users.FirstAsync(u => u.UserId == userId, ct);
        user.IsActive = s == StaffStatus.Active;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(userId, actorUserId, ct))!;
    }

    public async Task RemoveAsync(long actorUserId, long userId, CancellationToken ct = default)
    {
        await EnsureCanManageAsync(actorUserId, ct);
        var staff = await FindAsync(userId, ct) ?? throw NotFound();
        if (staff.AccessLevel == StaffAccess.Owner) throw new AppException("The owner can't be removed.");
        if (userId == actorUserId) throw new AppException("You can't remove your own access.");

        // Revoke admin access: drop the TenantStaff row + the ADMIN role, deactivate. Keep the User
        // (they may still be a customer). They lose all admin-panel access.
        db.TenantStaff.Remove(staff);
        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == AdminRole, ct);
        if (adminRole is not null)
        {
            var ur = await db.UserRoles.FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == adminRole.RoleId, ct);
            if (ur is not null) db.UserRoles.Remove(ur);
        }
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is not null) user.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers ----
    private Task<TenantStaff?> FindAsync(long userId, CancellationToken ct) =>
        db.TenantStaff.FirstOrDefaultAsync(s => s.UserId == userId, ct);

    private async Task EnsureCanManageAsync(long actorUserId, CancellationToken ct)
    {
        var actor = await FindAsync(actorUserId, ct);
        if (actor is null || !StaffAccess.CanManageStaff(actor.AccessLevel))
            throw new AppException("Only an owner or admin can manage staff.", StatusCodes.Status403Forbidden);
    }

    private async Task<StaffMember?> GetAsync(long userId, long currentUserId, CancellationToken ct) =>
        await (from ts in db.TenantStaff
               join u in db.Users on ts.UserId equals u.UserId
               where ts.UserId == userId
               select new StaffMember(u.UserId, u.FullName, u.Email, ts.AccessLevel, ts.Status,
                   u.LastLoginAt, ts.CreatedAt, u.UserId == currentUserId)).FirstOrDefaultAsync(ct);

    private static string NormalizeLevel(string level) =>
        StaffAccess.All.FirstOrDefault(l => l.Equals(level, StringComparison.OrdinalIgnoreCase))
        ?? throw new AppException("Unknown access level.");

    private static AppException NotFound() => new("Staff member not found.", StatusCodes.Status404NotFound);
}
