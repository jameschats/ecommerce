using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.SuperAdmin;

/// <summary>Super-admin authors platform announcements.</summary>
[ApiController]
[Route("api/superadmin/announcements")]
[Authorize(Roles = "SuperAdmin")]
public sealed class SuperAdminAnnouncementController(IPlatformAnnouncementService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<AnnouncementDto>>.Ok(await svc.ListAllAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AnnouncementUpsert req, CancellationToken ct)
        => Ok(ApiResponse<AnnouncementDto>.Ok(await svc.CreateAsync(req, ct), "Announcement posted."));

    [HttpPut("{id:long}/active")]
    public async Task<IActionResult> SetActive(long id, [FromBody] SetActiveRequest req, CancellationToken ct)
    {
        await svc.SetActiveAsync(id, req.Active, ct);
        return Ok(ApiResponse<object>.Ok(new { }, req.Active ? "Announcement shown." : "Announcement hidden."));
    }
}

/// <summary>Merchant admin reads the active broadcasts to show as banners.</summary>
[ApiController]
[Route("api/announcements")]
[Authorize(Roles = "Admin")]
public sealed class AnnouncementController(IPlatformAnnouncementService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Active(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<AnnouncementDto>>.Ok(await svc.ActiveAsync(DateTime.UtcNow, ct)));
}
