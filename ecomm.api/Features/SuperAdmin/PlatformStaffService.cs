using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.SuperAdmin;

public sealed record PlatformStaffDto(long UserId, string? Email, string? FullName, bool IsActive, DateTime? LastLoginAt, DateTime CreatedAt);
public sealed record CreateStaffRequest(string Email, string? FullName, string Password);

public interface IPlatformStaffService
{
    Task<IReadOnlyList<PlatformStaffDto>> ListAsync(CancellationToken ct);
    Task<PlatformStaffDto> CreateAsync(CreateStaffRequest req, long actingUserId, CancellationToken ct);
    Task SetActiveAsync(long userId, bool active, long actingUserId, CancellationToken ct);
}

/// <summary>
/// Platform operators = users holding the SuperAdmin role on the platform tenant (apex). A flat model:
/// every super-admin can invite/deactivate others. Granular per-permission tiers (Owner/Admin/Viewer) are a
/// deliberate later design — an under-specified RBAC split would be a security regression. Actions are audited.
/// </summary>
public sealed class PlatformStaffService(EcommerceDbContext db, IPasswordHasher hasher) : IPlatformStaffService
{
    private const string SuperAdminRole = "SUPERADMIN";

    public async Task<IReadOnlyList<PlatformStaffDto>> ListAsync(CancellationToken ct) =>
        await (from u in db.Users
               join ur in db.UserRoles on u.UserId equals ur.UserId
               join r in db.Roles on ur.RoleId equals r.RoleId
               where r.NormalizedName == SuperAdminRole && !u.IsDeleted
               orderby u.UserId
               select new PlatformStaffDto(u.UserId, u.Email, u.FullName, u.IsActive, u.LastLoginAt, u.CreatedAt))
            .ToListAsync(ct);

    public async Task<PlatformStaffDto> CreateAsync(CreateStaffRequest req, long actingUserId, CancellationToken ct)
    {
        var email = (req.Email ?? "").Trim();
        if (email.Length == 0 || !email.Contains('@')) throw new AppException("A valid email is required.", StatusCodes.Status400BadRequest);
        if ((req.Password ?? "").Length < 8) throw new AppException("Password must be at least 8 characters.", StatusCodes.Status400BadRequest);

        var norm = email.ToUpperInvariant();
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == norm && !u.IsDeleted, ct))
            throw new AppException("A user with that email already exists.", StatusCodes.Status409Conflict);
        var role = await db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == SuperAdminRole, ct)
                   ?? throw new AppException("SuperAdmin role is missing.", StatusCodes.Status500InternalServerError);

        var user = new User
        {
            Email = email, NormalizedEmail = norm, FullName = req.FullName?.Trim(),
            PasswordHash = hasher.Hash(req.Password!), IsActive = true, IsEmailVerified = true, CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);   // TenantId auto-stamped to the platform tenant

        db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId });
        db.PlatformAccessLog.Add(new PlatformAccessLog { AdminUserId = actingUserId, TenantId = null, Action = "CreateStaff", Detail = email, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);

        return new PlatformStaffDto(user.UserId, user.Email, user.FullName, user.IsActive, user.LastLoginAt, user.CreatedAt);
    }

    public async Task SetActiveAsync(long userId, bool active, long actingUserId, CancellationToken ct)
    {
        if (userId == actingUserId) throw new AppException("You can't change your own access.", StatusCodes.Status400BadRequest);
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct)
                   ?? throw new AppException("User not found.", StatusCodes.Status404NotFound);
        user.IsActive = active;
        user.UpdatedAt = DateTime.UtcNow;
        db.PlatformAccessLog.Add(new PlatformAccessLog { AdminUserId = actingUserId, TenantId = null, Action = active ? "ActivateStaff" : "DeactivateStaff", Detail = user.Email, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }
}
