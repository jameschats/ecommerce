using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Staff;

/// <summary>Merchant-admin Staff & roles. Panel access via ADMIN role; management restricted to Owner/Admin in the service.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/staff")]
public sealed class StaffAdminController(IStaffAdminService staff) : ControllerBase
{
    private long CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<StaffMember>>.Ok(await staff.ListAsync(CurrentUserId, ct)));

    [HttpGet("roles")]
    public IActionResult Roles()
        => Ok(ApiResponse<IReadOnlyList<StaffRoleInfo>>.Ok(staff.Roles()));

    [HttpPost]
    public async Task<IActionResult> Invite(InviteStaffRequest req, CancellationToken ct)
        => Ok(ApiResponse<StaffMember>.Ok(await staff.InviteAsync(CurrentUserId, req, ct), "Staff member added."));

    [HttpPut("{id:long}/role")]
    public async Task<IActionResult> UpdateRole(long id, UpdateStaffRoleRequest req, CancellationToken ct)
        => Ok(ApiResponse<StaffMember>.Ok(await staff.UpdateRoleAsync(CurrentUserId, id, req.AccessLevel, ct), "Role updated."));

    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> SetStatus(long id, [FromQuery] string status, CancellationToken ct)
        => Ok(ApiResponse<StaffMember>.Ok(await staff.SetStatusAsync(CurrentUserId, id, status, ct), "Status updated."));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Remove(long id, CancellationToken ct)
    { await staff.RemoveAsync(CurrentUserId, id, ct); return Ok(ApiResponse<object>.Ok(new { }, "Access removed.")); }
}
