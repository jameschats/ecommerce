using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Common.Security;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Identity;

public sealed record AdminUserDto(
    long UserId, string? FullName, string? Email, string? PhoneNumber,
    bool IsActive, DateTime CreatedAt, List<string> Roles);

public sealed record SetUserRolesRequest(List<string> Roles);

/// <summary>
/// People and what they may reach.
///
/// Nothing has ever listed users: the seeded admin was the only account with access, and
/// every registration is hardcoded to Customer. This is how a second person gets in.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = Perm.UserManage)]
public sealed class UsersAdminController : ControllerBase
{
    private const long Tenant = 1;

    private readonly EcommerceDbContext _db;
    public UsersAdminController(EcommerceDbContext db) => _db = db;

    /// <summary>
    /// Staff first, then everyone else. A shop has a handful of staff among thousands of
    /// customers, and the staff are who this screen exists to manage.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] bool staffOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var p = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, 100);

        var q = _db.Users.AsNoTracking().Where(u => u.TenantId == Tenant && !u.IsDeleted);

        if (staffOnly)
        {
            q = q.Where(u => _db.UserRoles
                .Any(ur => ur.UserId == u.UserId
                           && _db.Roles.Any(r => r.RoleId == ur.RoleId && r.NormalizedName != "CUSTOMER")));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u => (u.FullName != null && u.FullName.Contains(s))
                             || (u.Email != null && u.Email.Contains(s))
                             || (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
        }

        var total = await q.LongCountAsync(ct);
        var items = await q
            .OrderByDescending(u => u.UserId)
            .Skip((p - 1) * size).Take(size)
            .Select(u => new AdminUserDto(
                u.UserId, u.FullName, u.Email, u.PhoneNumber, u.IsActive, u.CreatedAt,
                _db.UserRoles.Where(ur => ur.UserId == u.UserId)
                    .Join(_db.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => r.Name)
                    .ToList()))
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<AdminUserDto>>.Ok(
            new PagedResult<AdminUserDto> { Items = items, Page = p, PageSize = size, TotalCount = total }));
    }

    [HttpPut("{userId:long}/roles")]
    public async Task<IActionResult> SetRoles(long userId, [FromBody] SetUserRolesRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.TenantId == Tenant, ct)
            ?? throw new AppException("User not found.", StatusCodes.Status404NotFound);

        var wanted = (req.Roles ?? []).Distinct().ToList();
        var roles = await _db.Roles.Where(r => r.TenantId == Tenant && wanted.Contains(r.Name)).ToListAsync(ct);

        // The last Admin cannot be demoted. Without this the shop can be locked out of its
        // own back office by one dropdown, with no way back in short of editing the database.
        var isAdminNow = await _db.UserRoles.AnyAsync(
            ur => ur.UserId == userId
                  && _db.Roles.Any(r => r.RoleId == ur.RoleId && r.NormalizedName == "ADMIN"), ct);
        var staysAdmin = roles.Any(r => r.NormalizedName == "ADMIN");

        if (isAdminNow && !staysAdmin)
        {
            var otherAdmins = await _db.UserRoles.CountAsync(
                ur => ur.UserId != userId
                      && _db.Roles.Any(r => r.RoleId == ur.RoleId && r.NormalizedName == "ADMIN"), ct);
            if (otherAdmins == 0)
                throw new AppException("This is the only administrator — give someone else the role first.");
        }

        var existing = await _db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync(ct);
        _db.UserRoles.RemoveRange(existing);
        foreach (var r in roles)
            _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = r.RoleId });

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(
            new { userId, roles = roles.Select(r => r.Name) },
            "Roles updated. They take effect when the person next signs in."));
    }
}
