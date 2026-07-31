using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Support;

/// <summary>Merchant support tickets (tenant-scoped).</summary>
[ApiController]
[Route("api/support/tickets")]
[Authorize(Roles = "Admin")]
public sealed class SupportController(ISupportService svc) : ControllerBase
{
    private long UserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<TicketDto>>.Ok(await svc.MyTicketsAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest req, CancellationToken ct)
        => Ok(ApiResponse<TicketDto>.Ok(await svc.CreateAsync(req.Subject, req.Message, UserId, ct), "Ticket created."));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Thread(long id, CancellationToken ct)
        => Ok(ApiResponse<TicketThreadDto>.Ok(await svc.ThreadAsync(id, ct)));

    [HttpPost("{id:long}/reply")]
    public async Task<IActionResult> Reply(long id, [FromBody] ReplyRequest req, CancellationToken ct)
    {
        await svc.ReplyAsync(id, req.Body, UserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Reply sent."));
    }
}

/// <summary>Platform support queue (cross-tenant).</summary>
[ApiController]
[Route("api/superadmin/support/tickets")]
[Authorize(Roles = "SuperAdmin")]
public sealed class SupportAdminController(ISupportService svc) : ControllerBase
{
    private long AdminUserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Queue(
        [FromQuery] string? status, [FromQuery] string? priority, [FromQuery] string? tier,
        [FromQuery] long? assignedTo, [FromQuery] bool mine = false, [FromQuery] bool unassigned = false,
        CancellationToken ct = default)
    {
        var agent = mine ? AdminUserId : assignedTo;
        var filter = new TicketQueueFilter(status, priority, tier, agent, unassigned);
        return Ok(ApiResponse<IReadOnlyList<TicketDto>>.Ok(await svc.QueueAsync(filter, ct)));
    }

    [HttpGet("agents")]
    public async Task<IActionResult> Agents(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<AgentDto>>.Ok(await svc.AgentsAsync(ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Thread(long id, CancellationToken ct)
        => Ok(ApiResponse<TicketThreadDto>.Ok(await svc.AdminThreadAsync(id, ct)));

    [HttpPost("{id:long}/reply")]
    public async Task<IActionResult> Reply(long id, [FromBody] AdminReplyRequest req, CancellationToken ct)
    {
        await svc.AdminReplyAsync(id, req.Body, AdminUserId, req.IsInternal, ct);
        return Ok(ApiResponse<object>.Ok(new { }, req.IsInternal ? "Note added." : "Reply sent."));
    }

    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> SetStatus(long id, [FromBody] StatusRequest req, CancellationToken ct)
        => Ok(ApiResponse<TicketDto>.Ok(await svc.SetStatusAsync(id, req.Status, AdminUserId, ct), "Status updated."));

    /// <summary>Set priority / category / assignee. Any omitted field is left as-is.</summary>
    [HttpPut("{id:long}/triage")]
    public async Task<IActionResult> Triage(long id, [FromBody] TriageRequest req, CancellationToken ct)
        => Ok(ApiResponse<TicketDto>.Ok(
            await svc.TriageAsync(id, req.Priority, req.Category, req.AssignedToUserId, AdminUserId, ct), "Ticket updated."));

    /// <summary>Escalate a tier (omit tier to step up one level).</summary>
    [HttpPut("{id:long}/escalate")]
    public async Task<IActionResult> Escalate(long id, [FromBody] EscalateRequest req, CancellationToken ct)
        => Ok(ApiResponse<TicketDto>.Ok(await svc.EscalateAsync(id, req.Tier, AdminUserId, ct), "Ticket escalated."));

    [HttpPut("{id:long}/assign")]
    public async Task<IActionResult> Assign(long id, [FromBody] AssignRequest req, CancellationToken ct)
        => Ok(ApiResponse<TicketDto>.Ok(await svc.AssignAsync(id, req.AssigneeUserId, AdminUserId, ct), "Ticket assigned."));

    [HttpPut("{id:long}/tags")]
    public async Task<IActionResult> Tags(long id, [FromBody] TagsRequest req, CancellationToken ct)
        => Ok(ApiResponse<TicketDto>.Ok(await svc.SetTagsAsync(id, req.Tags, AdminUserId, ct), "Tags updated."));
}

public sealed record CreateTicketRequest(string Subject, string Message);
public sealed record ReplyRequest(string Body);
public sealed record AdminReplyRequest(string Body, bool IsInternal);
public sealed record StatusRequest(string Status);
public sealed record TriageRequest(string? Priority, string? Category, long? AssignedToUserId);
public sealed record EscalateRequest(string? Tier);
public sealed record AssignRequest(long? AssigneeUserId);
public sealed record TagsRequest(string? Tags);
