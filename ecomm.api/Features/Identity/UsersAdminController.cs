using System.Security.Claims;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Common.Security;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Identity;

public sealed record AdminUserDto(
    long UserId, string? FullName, string? Email, string? PhoneNumber,
    bool IsActive, DateTime CreatedAt, List<string> Roles);

public sealed record SetUserRolesRequest(List<string> Roles);

public sealed record CreateStaffRequest(
    string? FullName, string? Email, string? PhoneNumber, string? Password, List<string>? Roles);

public sealed record UpdateStaffRequest(string? FullName, string? Email, string? PhoneNumber);

public sealed record SetActiveRequest(bool IsActive);

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
    private readonly IPasswordHasher _hasher;
    private readonly IAuthService _auth;

    public UsersAdminController(EcommerceDbContext db, IPasswordHasher hasher, IAuthService auth)
    {
        _db = db;
        _hasher = hasher;
        _auth = auth;
    }

    /// <summary>The signed-in admin, so they cannot switch off or delete themselves.</summary>
    private long CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

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

        // Rights taken away should not survive in a token that is still being renewed.
        await RevokeSessionsAsync(userId, ct);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(
            new { userId, roles = roles.Select(r => r.Name) },
            "Roles updated. They take effect when the person next signs in."));
    }

    // ---------------- Create / edit / retire ----------------

    /// <summary>
    /// Add a staff member. Registration is customer-only by design, so this is the one way a
    /// second person gets into the back office.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStaffRequest req, CancellationToken ct)
    {
        var email = (req.Email ?? "").Trim();
        var phone = string.IsNullOrWhiteSpace(req.PhoneNumber) ? null : req.PhoneNumber.Trim();
        var password = req.Password ?? "";

        if (string.IsNullOrWhiteSpace(email)) throw new AppException("Email is required.");
        if (password.Length < 6) throw new AppException("Password must be at least 6 characters.");

        var wanted = (req.Roles ?? []).Distinct().ToList();
        if (wanted.Count == 0) throw new AppException("Choose at least one role.");

        // Someone who already shops here is the usual case, and they do not need a second
        // account — they need a role. Say so, rather than just refusing.
        await RequireEmailFreeAsync(email, null, ct,
            "Someone already uses this email. To give them access, tick a role against their name in the list below.");
        await RequirePhoneFreeAsync(phone, null, ct);

        var roles = await _db.Roles.Where(r => r.TenantId == Tenant && wanted.Contains(r.Name)).ToListAsync(ct);
        if (roles.Count != wanted.Count) throw new AppException("One of those roles no longer exists.");

        var now = DateTime.UtcNow;
        var user = new User
        {
            TenantId = Tenant,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = _hasher.Hash(password),
            FullName = string.IsNullOrWhiteSpace(req.FullName) ? null : req.FullName.Trim(),
            PhoneNumber = phone,
            IsActive = true,
            CreatedAt = now,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        foreach (var r in roles)
            _db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = r.RoleId });
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(
            new { userId = user.UserId },
            "Staff member added. Ask them to change the password after their first sign-in."));
    }

    [HttpPut("{userId:long}")]
    public async Task<IActionResult> Update(long userId, [FromBody] UpdateStaffRequest req, CancellationToken ct)
    {
        var user = await LoadStaffAsync(userId, ct);

        var email = (req.Email ?? "").Trim();
        var phone = string.IsNullOrWhiteSpace(req.PhoneNumber) ? null : req.PhoneNumber.Trim();
        if (string.IsNullOrWhiteSpace(email)) throw new AppException("Email is required.");

        await RequireEmailFreeAsync(email, userId, ct);
        await RequirePhoneFreeAsync(phone, userId, ct);

        // A changed address is an unproven one — carrying the old verification across would
        // vouch for something nobody has checked.
        if (!string.Equals(user.NormalizedEmail, email.ToUpperInvariant(), StringComparison.Ordinal))
        {
            user.IsEmailVerified = false;
            user.EmailVerifiedAt = null;
        }

        user.FullName = string.IsNullOrWhiteSpace(req.FullName) ? null : req.FullName.Trim();
        user.Email = email;
        user.NormalizedEmail = email.ToUpperInvariant();
        user.PhoneNumber = phone;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { userId }, "Saved."));
    }

    [HttpPut("{userId:long}/active")]
    public async Task<IActionResult> SetActive(long userId, [FromBody] SetActiveRequest req, CancellationToken ct)
    {
        var user = await LoadStaffAsync(userId, ct);
        if (!req.IsActive) await RequireNotLastWayInAsync(user, "switch off", ct);

        user.IsActive = req.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        if (!req.IsActive) await RevokeSessionsAsync(userId, ct);

        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { userId, isActive = user.IsActive },
            req.IsActive ? "Account switched back on." : "Account switched off."));
    }

    /// <summary>
    /// Soft delete. Orders point at this row, so removing it outright would orphan them —
    /// and the email and phone are released, because a soft-deleted account otherwise holds
    /// its address forever and the same person can never be added back.
    /// </summary>
    [HttpDelete("{userId:long}")]
    public async Task<IActionResult> Delete(long userId, CancellationToken ct)
    {
        var user = await LoadStaffAsync(userId, ct);
        await RequireNotLastWayInAsync(user, "delete", ct);

        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            user.Email = $"deleted+{userId}.{stamp}@{user.Email.Split('@').Last()}";
            user.NormalizedEmail = user.Email.ToUpperInvariant();
        }
        user.PhoneNumber = null;   // frees uq_users_tenant_phone
        user.IsEmailVerified = false;
        user.IsPhoneVerified = false;
        user.IsActive = false;
        user.IsDeleted = true;
        user.UpdatedAt = DateTime.UtcNow;

        _db.UserRoles.RemoveRange(await _db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync(ct));
        await RevokeSessionsAsync(userId, ct);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { userId }, "Deleted. Their email and phone are free to use again."));
    }

    /// <summary>
    /// Sends the ordinary reset email, so an admin never has to handle a password.
    ///
    /// The conditions are checked here rather than left to the service. RequestPasswordResetAsync
    /// is deliberately silent — it no-ops for unknown, inactive or passwordless accounts and
    /// swallows rate-limit errors so the public forgot-password endpoint cannot be used to
    /// discover who has an account. That is right for a stranger and wrong for an admin: telling
    /// them "sent" when nothing was sent leaves them waiting on an email that is not coming.
    /// </summary>
    [HttpPost("{userId:long}/send-reset")]
    public async Task<IActionResult> SendReset(long userId, CancellationToken ct)
    {
        var user = await LoadStaffAsync(userId, ct);

        if (string.IsNullOrWhiteSpace(user.Email))
            throw new AppException("This account has no email address to send to.");
        if (!user.IsActive)
            throw new AppException("This account is switched off — switch it back on first.");
        if (string.IsNullOrEmpty(user.PasswordHash))
            throw new AppException("This account signs in by OTP or Google, so there is no password to reset.");

        await _auth.RequestPasswordResetAsync(user.Email, ct);
        return Ok(ApiResponse<object>.Ok(new { userId }, $"Reset link sent to {user.Email}."));
    }

    // ---------------- Guards ----------------

    /// <summary>
    /// Create, edit and delete apply to staff only. Customers stay listed and their roles
    /// stay assignable, but their own details are theirs to change.
    /// </summary>
    private async Task<User> LoadStaffAsync(long userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.UserId == userId && u.TenantId == Tenant && !u.IsDeleted, ct)
            ?? throw new AppException("User not found.", StatusCodes.Status404NotFound);

        // The test is "is a customer", not "has a staff role". An account with no roles at all
        // is a staff member somebody stripped — refusing to touch it would strand it, editable
        // by nobody and deletable by nobody. Every self-registered customer holds the Customer
        // role (AuthService assigns it), so this cannot let a real customer through.
        var roles = await _db.UserRoles.Where(ur => ur.UserId == userId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => r.NormalizedName)
            .ToListAsync(ct);
        if (roles.Count > 0 && roles.All(r => r == "CUSTOMER"))
            throw new AppException("This is a customer account — give them a staff role first.");

        return user;
    }

    private async Task RequireEmailFreeAsync(
        string email, long? exceptUserId, CancellationToken ct, string? message = null)
    {
        var normalized = email.ToUpperInvariant();
        // Deleted rows are excluded deliberately: Delete releases the address, so a name that
        // is only held by a deleted account must be usable again.
        if (await _db.Users.AnyAsync(u => u.TenantId == Tenant && !u.IsDeleted
                                          && u.NormalizedEmail == normalized
                                          && (exceptUserId == null || u.UserId != exceptUserId), ct))
            throw new AppException(
                message ?? "An account with this email already exists.", StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// Checked rather than caught: uq_users_tenant_phone once surfaced as "an unexpected error
    /// occurred" partway through placing an order. A clash should say whose number it is.
    /// </summary>
    private async Task RequirePhoneFreeAsync(string? phone, long? exceptUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone)) return;
        if (await _db.Users.AnyAsync(u => u.TenantId == Tenant && !u.IsDeleted
                                          && u.PhoneNumber == phone
                                          && (exceptUserId == null || u.UserId != exceptUserId), ct))
            throw new AppException("Another account already uses this mobile number.", StatusCodes.Status409Conflict);
    }

    /// <summary>Refuses to remove the last administrator, or the one doing the removing.</summary>
    private async Task RequireNotLastWayInAsync(User user, string verb, CancellationToken ct)
    {
        if (user.UserId == CurrentUserId)
            throw new AppException($"You cannot {verb} your own account.");

        var isAdmin = await _db.UserRoles.AnyAsync(
            ur => ur.UserId == user.UserId
                  && _db.Roles.Any(r => r.RoleId == ur.RoleId && r.NormalizedName == "ADMIN"), ct);
        if (!isAdmin) return;

        var otherAdmins = await _db.UserRoles.CountAsync(
            ur => ur.UserId != user.UserId
                  && _db.Roles.Any(r => r.RoleId == ur.RoleId && r.NormalizedName == "ADMIN")
                  && _db.Users.Any(u => u.UserId == ur.UserId && u.IsActive && !u.IsDeleted), ct);
        if (otherAdmins == 0)
            throw new AppException($"This is the only administrator — you cannot {verb} them.");
    }

    /// <summary>
    /// Ends every live session. Refresh is a rolling grant, so without this a change of rights
    /// or a switched-off account keeps renewing itself indefinitely.
    /// </summary>
    private async Task RevokeSessionsAsync(long userId, CancellationToken ct)
    {
        var live = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var t in live) t.RevokedAt = now;
    }
}
