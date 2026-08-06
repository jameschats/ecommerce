using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Common.Security;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Identity;

public sealed record PermissionDto(long PermissionId, string Code, string Name, string Module);
public sealed record RoleDto(long RoleId, string Name, string? Description, bool IsSystem, List<string> Permissions);

/// <summary>The access matrix: every module, every role, and which permissions are granted.</summary>
public sealed record AccessMatrixDto(
    List<RoleDto> Roles,
    List<PermissionDto> Permissions,
    Dictionary<string, string> ModulePages);

public sealed record SetRolePermissionsRequest(List<string> Permissions);

/// <summary>
/// Roles and what they can reach.
///
/// The schema for this has existed since migration 002 with no screen anywhere, so the only
/// way to see who could do what was to read SQL. This renders it as a grid because that is
/// the shape of the question — modules down, roles across.
/// </summary>
[ApiController]
[Route("api/admin/roles")]
[Authorize(Policy = Perm.RoleManage)]
public sealed class RolesAdminController : ControllerBase
{
    private const long Tenant = 1;

    /// <summary>
    /// Which admin screen each module governs. Anna thinks in screens, not permission codes,
    /// so the matrix names the page a row controls.
    /// </summary>
    private static readonly Dictionary<string, string> ModulePages = new()
    {
        ["Catalog"] = "/admin/products",
        ["Inventory"] = "/admin/inventory",
        ["Orders"] = "/admin/orders",
        ["Payments"] = "/admin/payments",
        ["Customers"] = "/admin/contacts",
        ["Coupons"] = "/admin/coupons",
        ["Reviews"] = "/admin/reviews",
        ["Cms"] = "/admin/home-page",
        ["Theme"] = "/admin/theme",
        ["Settings"] = "/admin/store-settings",
        ["Media"] = "/admin/banners",
        ["ImportExport"] = "/admin/import",
        ["Reports"] = "/admin/analytics",
        ["Identity"] = "/admin/users",
    };

    private readonly EcommerceDbContext _db;
    public RolesAdminController(EcommerceDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Matrix(CancellationToken ct)
    {
        var permissions = await _db.Permissions
            .OrderBy(p => p.Module).ThenBy(p => p.Code)
            .Select(p => new PermissionDto(p.PermissionId, p.Code, p.Name, p.Module))
            .ToListAsync(ct);

        var roles = await _db.Roles
            .Where(r => r.TenantId == Tenant)
            .OrderBy(r => r.IsSystem ? 0 : 1).ThenBy(r => r.Name)
            .Select(r => new RoleDto(
                r.RoleId, r.Name, r.Description, r.IsSystem,
                _db.RolePermissions.Where(rp => rp.RoleId == r.RoleId)
                    .Join(_db.Permissions, rp => rp.PermissionId, p => p.PermissionId, (rp, p) => p.Code)
                    .ToList()))
            .ToListAsync(ct);

        return Ok(ApiResponse<AccessMatrixDto>.Ok(new AccessMatrixDto(roles, permissions, ModulePages)));
    }

    /// <summary>Replaces a role's permissions wholesale — the matrix always sends the full set.</summary>
    [HttpPut("{roleId:long}/permissions")]
    public async Task<IActionResult> SetPermissions(
        long roleId, [FromBody] SetRolePermissionsRequest req, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId && r.TenantId == Tenant, ct)
            ?? throw new AppException("Role not found.", StatusCodes.Status404NotFound);

        // Admin and Customer are structural. Editing Admin is how an owner locks themselves
        // out of their own shop with one careless click, and Customer holds nothing by design.
        if (role.IsSystem)
            throw new AppException($"{role.Name} is a built-in role and cannot be changed.");

        var codes = (req.Permissions ?? []).Distinct().ToList();
        var ids = await _db.Permissions.Where(p => codes.Contains(p.Code))
            .Select(p => p.PermissionId).ToListAsync(ct);

        var existing = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        _db.RolePermissions.RemoveRange(existing);
        foreach (var id in ids)
            _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = id });

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(
            new { roleId, count = ids.Count },
            "Access updated. The person will see the change after signing in again."));
    }
}
