using System.Globalization;
using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.SuperAdmin;

/// <summary>Platform owner — cross-tenant management. SuperAdmin role only.</summary>
[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "SuperAdmin")]
public sealed class SuperAdminController(ISuperAdminService svc, ecomm.api.Features.Apps.IAppService apps) : ControllerBase
{
    private long AdminUserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    // App marketplace — register/list first-party apps (S1).
    [HttpGet("apps")]
    public async Task<IActionResult> Apps(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ecomm.api.Features.Apps.AppListingDto>>.Ok(await apps.ListAllForAdminAsync(ct)));

    [HttpPost("apps")]
    public async Task<IActionResult> RegisterApp([FromBody] ecomm.api.Features.Apps.RegisterAppRequest req, CancellationToken ct)
        => Ok(ApiResponse<ecomm.api.Features.Apps.RegisteredAppDto>.Ok(await apps.RegisterFirstPartyAppAsync(req, AdminUserId, ct), "App registered — copy the client secret now, it won't be shown again."));

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

    [HttpGet("analytics")]
    public async Task<IActionResult> Analytics([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(ApiResponse<PlatformAnalyticsDto>.Ok(await svc.PlatformAnalyticsAsync(f, t, ct)));
    }

    // Parse yyyy-MM-dd; default to the last 30 days, `to` inclusive (end of day).
    private static (DateTime from, DateTime to) Range(string? from, string? to)
    {
        var toDate = TryDate(to) ?? DateTime.UtcNow.Date;
        var fromDate = TryDate(from) ?? toDate.AddDays(-29);
        return (fromDate.Date, toDate.Date.AddDays(1).AddTicks(-1));
    }
    private static DateTime? TryDate(string? s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : null;

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

    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<PlanDto>>.Ok(await svc.ListPlansAsync(ct)));

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromBody] PlanUpsert req, CancellationToken ct)
        => Ok(ApiResponse<PlanDto>.Ok(await svc.CreatePlanAsync(req, AdminUserId, ct), "Plan created."));

    [HttpPut("plans/{planId:int}")]
    public async Task<IActionResult> UpdatePlan(int planId, [FromBody] PlanUpsert req, CancellationToken ct)
        => Ok(ApiResponse<PlanDto>.Ok(await svc.UpdatePlanAsync(planId, req, AdminUserId, ct), "Plan saved."));

    [HttpGet("credit-packs")]
    public async Task<IActionResult> Packs(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<AiCreditPackDto>>.Ok(await svc.ListPacksAsync(ct)));

    [HttpPost("credit-packs")]
    public async Task<IActionResult> CreatePack([FromBody] PackUpsert req, CancellationToken ct)
        => Ok(ApiResponse<AiCreditPackDto>.Ok(await svc.CreatePackAsync(req, AdminUserId, ct), "Pack created."));

    [HttpPut("credit-packs/{packId:int}")]
    public async Task<IActionResult> UpdatePack(int packId, [FromBody] PackUpsert req, CancellationToken ct)
        => Ok(ApiResponse<AiCreditPackDto>.Ok(await svc.UpdatePackAsync(packId, req, AdminUserId, ct), "Pack saved."));

    [HttpPost("tenants/{id:long}/grant-credits")]
    public async Task<IActionResult> GrantCredits(long id, [FromBody] GrantCreditsRequest req, CancellationToken ct)
    {
        await svc.GrantCreditsAsync(id, req.Amount, req.Reason, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, $"Granted {req.Amount} credits."));
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> Subscriptions([FromQuery] string? status, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<SubStatusRow>>.Ok(await svc.SubscriptionsAsync(status, ct)));

    [HttpGet("charges")]
    public async Task<IActionResult> Charges([FromQuery] int limit = 100, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<BillingChargeDto>>.Ok(await svc.RecentChargesAsync(limit, ct)));

    [HttpPost("tenants/{id:long}/record-payment")]
    public async Task<IActionResult> RecordPayment(long id, [FromBody] RecordPaymentRequest req, CancellationToken ct)
    {
        await svc.RecordManualPaymentAsync(id, req.PlanId, req.Amount, req.Reference, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Payment recorded — subscription active."));
    }

    [HttpPut("tenants/{id:long}/plan")]
    public async Task<IActionResult> ChangePlan(long id, [FromBody] ChangePlanRequest req, CancellationToken ct)
    {
        await svc.ChangePlanAsync(id, req.PlanId, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Plan changed."));
    }

    [HttpPut("tenants/{id:long}/trial")]
    public async Task<IActionResult> SetTrial(long id, [FromBody] SetTrialRequest req, CancellationToken ct)
    {
        await svc.SetTrialAsync(id, req.TrialEndsAt, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Trial updated."));
    }

    [HttpPut("tenants/{id:long}/tags")]
    public async Task<IActionResult> SetTags(long id, [FromBody] SetTagsRequest req, CancellationToken ct)
    {
        await svc.SetTagsAsync(id, req.Tags, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Tags saved."));
    }

    [HttpPost("tenants/{id:long}/notes")]
    public async Task<IActionResult> AddNote(long id, [FromBody] AddNoteRequest req, CancellationToken ct)
    {
        await svc.AddNoteAsync(id, req.Note, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Note added."));
    }

    [HttpPost("tenants/{id:long}/offboard")]
    public async Task<IActionResult> Offboard(long id, CancellationToken ct)
    {
        await svc.OffboardAsync(id, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Store off-boarded."));
    }

    /// <summary>Act as a store: mode=view (read-only, default) or full. Returns a short-lived token + the store URL.</summary>
    [HttpPost("tenants/{id:long}/impersonate")]
    public async Task<IActionResult> Impersonate(long id, [FromQuery] string mode = "view", CancellationToken ct = default)
        => Ok(ApiResponse<ImpersonationResult>.Ok(await svc.ImpersonateAsync(id, mode, AdminUserId, ct)));

    [HttpGet("blocklist")]
    public async Task<IActionResult> Blocklist(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<BlocklistDto>>.Ok(await svc.ListBlocklistAsync(ct)));

    [HttpPost("blocklist")]
    public async Task<IActionResult> AddBlock([FromBody] AddBlockRequest req, CancellationToken ct)
    {
        await svc.AddBlockAsync(req.Type, req.Value, req.Reason, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Added to blocklist."));
    }

    [HttpDelete("blocklist/{id:long}")]
    public async Task<IActionResult> RemoveBlock(long id, CancellationToken ct)
    {
        await svc.RemoveBlockAsync(id, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Removed from blocklist."));
    }

    [HttpGet("payment-settings")]
    public async Task<IActionResult> PlatformPayment(CancellationToken ct)
        => Ok(ApiResponse<PlatformPaymentDto>.Ok(await svc.GetPlatformPaymentAsync(ct)));

    [HttpPut("payment-settings")]
    public async Task<IActionResult> SavePlatformPayment([FromBody] PlatformPaymentUpsert req, CancellationToken ct)
        => Ok(ApiResponse<PlatformPaymentDto>.Ok(await svc.SavePlatformPaymentAsync(req, AdminUserId, ct), "Payment settings saved."));

    [HttpPost("refund")]
    public async Task<IActionResult> RefundCharge([FromBody] RefundChargeReq req, CancellationToken ct)
    {
        await svc.RefundSubscriptionChargeAsync(req.BillingHistoryId, req.Amount, req.Reason, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Refund issued and credit note created."));
    }

    [HttpGet("billing-settings")]
    public async Task<IActionResult> PlatformBilling(CancellationToken ct)
        => Ok(ApiResponse<PlatformBillingDto>.Ok(await svc.GetPlatformBillingAsync(ct)));

    [HttpPut("billing-settings")]
    public async Task<IActionResult> SavePlatformBilling([FromBody] PlatformBillingUpsert req, CancellationToken ct)
        => Ok(ApiResponse<PlatformBillingDto>.Ok(await svc.SavePlatformBillingAsync(req, AdminUserId, ct), "Billing settings saved."));

    [HttpGet("email-settings")]
    public async Task<IActionResult> PlatformEmail(CancellationToken ct)
        => Ok(ApiResponse<PlatformEmailDto>.Ok(await svc.GetPlatformEmailAsync(ct)));

    [HttpPut("email-settings")]
    public async Task<IActionResult> SavePlatformEmail([FromBody] PlatformEmailUpsert req, CancellationToken ct)
        => Ok(ApiResponse<PlatformEmailDto>.Ok(await svc.SavePlatformEmailAsync(req, AdminUserId, ct), "Email settings saved."));

    [HttpPost("email-settings/test")]
    public async Task<IActionResult> SendTestEmail([FromBody] TestEmailReq req, CancellationToken ct)
    {
        await svc.SendTestEmailAsync(req.To, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Test email sent."));
    }

    [HttpGet("tenants/{id:long}/diagnostics")]
    public async Task<IActionResult> Diagnostics(long id, CancellationToken ct)
        => Ok(ApiResponse<TenantDiagnosticsDto>.Ok(await svc.DiagnosticsAsync(id, ct)));

    [HttpPost("notifications/{historyId:long}/resend")]
    public async Task<IActionResult> ResendNotification(long historyId, CancellationToken ct)
    {
        await svc.ResendNotificationAsync(historyId, AdminUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Notification re-sent."));
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] long? tenantId, [FromQuery] int limit = 100, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<AuditDto>>.Ok(await svc.GetAuditAsync(tenantId, limit, ct)));
}

public sealed record SetStandingRequest(string Standing, string? Reason);
public sealed record AddBlockRequest(string Type, string Value, string? Reason);
public sealed record ChangePlanRequest(int PlanId);
public sealed record SetTrialRequest(DateTime? TrialEndsAt);
public sealed record SetTagsRequest(string? Tags);
public sealed record AddNoteRequest(string Note);
public sealed record GrantCreditsRequest(int Amount, string? Reason);
public sealed record RecordPaymentRequest(int PlanId, decimal Amount, string? Reference);
public sealed record RefundChargeReq(long BillingHistoryId, decimal Amount, string? Reason);
