using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.SuperAdmin;

/// <summary>Platform owner — cross-tenant management. SuperAdmin role only.</summary>
[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "SuperAdmin")]
public sealed class SuperAdminController(ISuperAdminService svc) : ControllerBase
{
    private long AdminUserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    [HttpGet("tenants")]
    public async Task<IActionResult> Tenants([FromQuery] string? search, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<TenantSummaryDto>>.Ok(await svc.ListTenantsAsync(search, ct)));

    [HttpGet("tenants/{id:long}")]
    public async Task<IActionResult> Tenant(long id, CancellationToken ct)
    {
        var t = await svc.GetTenantAsync(id, AdminUserId, ct);
        return t is null ? NotFound(ApiResponse<object>.Fail("Tenant not found.")) : Ok(ApiResponse<TenantDetailDto>.Ok(t));
    }

    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue(CancellationToken ct)
        => Ok(ApiResponse<PlatformRevenueDto>.Ok(await svc.GetRevenueAsync(ct)));

    [HttpPut("tenants/{id:long}/standing")]
    public async Task<IActionResult> SetStanding(long id, [FromBody] SetStandingRequest req, CancellationToken ct)
    {
        await svc.SetStandingAsync(id, req.Standing, req.Reason, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Standing updated."));
    }

    [HttpPost("tenants/{id:long}/suspend")]
    public async Task<IActionResult> Suspend(long id, CancellationToken ct)
    {
        await svc.SetActiveAsync(id, false, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Tenant suspended."));
    }

    [HttpPost("tenants/{id:long}/activate")]
    public async Task<IActionResult> Activate(long id, CancellationToken ct)
    {
        await svc.SetActiveAsync(id, true, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Tenant activated."));
    }
}

public sealed record SetStandingRequest(string Standing, string? Reason);
