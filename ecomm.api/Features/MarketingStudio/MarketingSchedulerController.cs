using ecomm.api.Common.Models;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.MarketingStudio;

public sealed record RescheduleRequest(DateTime ScheduledAt);

/// <summary>
/// The scheduler screen (MS2 sub-step 4): list per-channel scheduled posts (also serves as job
/// history), approve them through the D5 gate, reschedule, skip or delete. Publishing itself is done
/// by the Hangfire sweep. Admin + feature-gated; part of the Marketing Studio module (/api/marketing/*).
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[RequiresFeature("marketing_studio")]
[Route("api/marketing/scheduler")]
public sealed class MarketingSchedulerController(IMarketingSchedulerService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ScheduledPostDto>>.Ok(await svc.ListAsync(status, ct)));

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken ct)
        => Ok(ApiResponse<ScheduledPostDto>.Ok(await svc.ApproveAsync(id, ct), "Approved."));

    [HttpPost("approve-all")]
    public async Task<IActionResult> ApproveAll(CancellationToken ct)
        => Ok(ApiResponse<object>.Ok(new { approved = await svc.ApproveAllPendingAsync(ct) }, "Approved all pending posts."));

    [HttpPut("{id:long}/reschedule")]
    public async Task<IActionResult> Reschedule(long id, RescheduleRequest req, CancellationToken ct)
        => Ok(ApiResponse<ScheduledPostDto>.Ok(await svc.RescheduleAsync(id, req.ScheduledAt, ct), "Rescheduled."));

    [HttpPost("{id:long}/skip")]
    public async Task<IActionResult> Skip(long id, CancellationToken ct)
        => Ok(ApiResponse<ScheduledPostDto>.Ok(await svc.SkipAsync(id, ct), "Skipped."));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await svc.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Deleted."));
    }
}
