using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.SuperAdmin;

/// <summary>Platform staff (super-admin operators). SuperAdmin role only.</summary>
[ApiController]
[Route("api/superadmin/staff")]
[Authorize(Roles = "SuperAdmin")]
public sealed class PlatformStaffController(IPlatformStaffService staff) : ControllerBase
{
    private long ActingUserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<PlatformStaffDto>>.Ok(await staff.ListAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStaffRequest req, CancellationToken ct)
        => Ok(ApiResponse<PlatformStaffDto>.Ok(await staff.CreateAsync(req, ActingUserId, ct), "Staff member added."));

    [HttpPut("{userId:long}/active")]
    public async Task<IActionResult> SetActive(long userId, [FromBody] SetActiveRequest req, CancellationToken ct)
    {
        await staff.SetActiveAsync(userId, req.Active, ActingUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, req.Active ? "Access restored." : "Access revoked."));
    }
}

public sealed record SetActiveRequest(bool Active);
