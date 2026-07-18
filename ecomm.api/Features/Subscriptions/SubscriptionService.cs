using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Subscriptions;

public sealed record SubscriptionDto(
    string Status, int PlanId, string PlanName, string PlanSlug, decimal MonthlyPrice,
    DateTime? CurrentPeriodEnd, DateTime? GraceEndsAt, bool IsActive, bool IsInTrial);

public sealed record BillingHistoryDto(
    long Id, decimal Amount, string Status, DateTime BilledAt, DateTime? PeriodStart, DateTime? PeriodEnd, string? Reference);

public sealed record RecordChargeCommand(
    long TenantId, decimal Amount, string RazorpayPaymentId,
    string? RazorpaySubscriptionId, DateTime PeriodStart, DateTime PeriodEnd);

/// <summary>What the browser needs to open the platform's Razorpay checkout for one billing cycle.</summary>
public sealed record CheckoutSessionDto(string GatewayOrderId, decimal Amount, string Currency, string? KeyId, string Provider, int PlanId, string PlanName);
public sealed record ConfirmCheckoutCommand(int PlanId, string GatewayOrderId, string PaymentId, string Signature);

public interface ISubscriptionService
{
    Task<SubscriptionDto?> GetCurrentAsync(CancellationToken ct);          // merchant (current tenant)
    Task<IReadOnlyList<BillingHistoryDto>> GetBillingHistoryAsync(CancellationToken ct);   // merchant invoices
    Task<SubscriptionDto> SelectPlanAsync(int planId, CancellationToken ct);
    Task CancelAsync(CancellationToken ct);
    Task<bool> RecordChargeAsync(RecordChargeCommand cmd, CancellationToken ct);   // webhook, idempotent
    Task<int> RunLifecycleSweepAsync(DateTime nowUtc, int graceDays, CancellationToken ct);   // background
    Task<CheckoutSessionDto> StartCheckoutAsync(int planId, CancellationToken ct);            // pay one cycle
    Task<SubscriptionDto> ConfirmCheckoutAsync(ConfirmCheckoutCommand cmd, CancellationToken ct);
}

public sealed class SubscriptionService(
    EcommerceDbContext db,
    ecomm.api.Features.Payments.PlatformPaymentGatewayFactory gateways,
    ecomm.api.Common.Tenancy.ICurrentTenantService tenant) : ISubscriptionService
{
    public const string Trial = "Trial", Active = "Active", PastDue = "PastDue", Suspended = "Suspended", Cancelled = "Cancelled";

    public async Task<SubscriptionDto?> GetCurrentAsync(CancellationToken ct)
    {
        var s = await db.TenantSubscriptions.Include(x => x.Plan)
            .OrderByDescending(x => x.TenantSubscriptionId).FirstOrDefaultAsync(ct);
        return s is null ? null : ToDto(s);
    }

    public async Task<IReadOnlyList<BillingHistoryDto>> GetBillingHistoryAsync(CancellationToken ct) =>
        await db.TenantBillingHistory.AsNoTracking()
            .OrderByDescending(b => b.BilledAt)
            .Select(b => new BillingHistoryDto(
                b.TenantBillingHistoryId, b.Amount, b.Status, b.BilledAt, b.PeriodStart, b.PeriodEnd, b.RazorpayPaymentId))
            .ToListAsync(ct);

    public async Task<SubscriptionDto> SelectPlanAsync(int planId, CancellationToken ct)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive, ct)
                   ?? throw new AppException("Plan not found.", StatusCodes.Status404NotFound);

        var sub = await db.TenantSubscriptions.OrderByDescending(x => x.TenantSubscriptionId).FirstOrDefaultAsync(ct);
        if (sub is null)
        {
            sub = new TenantSubscription { PlanId = plan.PlanId, Status = Trial, CreatedAt = DateTime.UtcNow };
            db.TenantSubscriptions.Add(sub);   // TenantId auto-stamped
        }
        else
        {
            sub.PlanId = plan.PlanId;
            sub.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        // A real flow now creates a Razorpay Subscription and redirects to checkout; the
        // charge activates the plan via the webhook (RecordChargeAsync).
        return ToDto(await db.TenantSubscriptions.Include(x => x.Plan).FirstAsync(x => x.TenantSubscriptionId == sub.TenantSubscriptionId, ct));
    }

    /// <summary>
    /// Start a one-cycle checkout against the PLATFORM's Razorpay account (merchant pays us).
    /// Returns what the browser widget needs. Mock gateway in dev → KeyId is null and the client
    /// confirms straight away (MockPaymentGateway.VerifySignature always passes).
    /// </summary>
    public async Task<CheckoutSessionDto> StartCheckoutAsync(int planId, CancellationToken ct)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive, ct)
                   ?? throw new AppException("Plan not found.", StatusCodes.Status404NotFound);
        if (plan.MonthlyPrice <= 0) throw new AppException("That plan is free — just select it.", StatusCodes.Status400BadRequest);

        var gateway = gateways.Create();
        var order = await gateway.CreateOrderAsync(db.CurrentTenantId, plan.MonthlyPrice, "INR", $"sub-{db.CurrentTenantId}-{planId}", ct);
        return new CheckoutSessionDto(order.GatewayOrderId, order.Amount, order.Currency, gateway.PublicKey, gateway.Name, plan.PlanId, plan.Name);
    }

    /// <summary>
    /// Verify the Razorpay signature, then record the charge + activate the plan for one month.
    /// The amount is taken from the PLAN, never the client. Idempotent on the payment id, so the
    /// billing webhook delivering the same payment afterwards is a safe no-op.
    /// </summary>
    public async Task<SubscriptionDto> ConfirmCheckoutAsync(ConfirmCheckoutCommand cmd, CancellationToken ct)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.PlanId == cmd.PlanId && p.IsActive, ct)
                   ?? throw new AppException("Plan not found.", StatusCodes.Status404NotFound);
        if (string.IsNullOrWhiteSpace(cmd.PaymentId)) throw new AppException("Missing payment id.", StatusCodes.Status400BadRequest);

        var gateway = gateways.Create();
        if (!gateway.VerifySignature(cmd.GatewayOrderId, cmd.PaymentId, cmd.Signature))
            throw new AppException("Payment verification failed.", StatusCodes.Status400BadRequest);

        await SelectPlanAsync(cmd.PlanId, ct);   // point the subscription at the plan being paid for
        var now = DateTime.UtcNow;
        await RecordChargeAsync(new RecordChargeCommand(db.CurrentTenantId, plan.MonthlyPrice, cmd.PaymentId, null, now, now.AddMonths(1)), ct);
        return (await GetCurrentAsync(ct))!;
    }

    public async Task CancelAsync(CancellationToken ct)
    {
        var sub = await db.TenantSubscriptions.OrderByDescending(x => x.TenantSubscriptionId).FirstOrDefaultAsync(ct);
        if (sub is null) return;
        sub.Status = Cancelled;
        sub.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Webhook path. Idempotent on RazorpayPaymentId. Cross-tenant → no query filter.</summary>
    public async Task<bool> RecordChargeAsync(RecordChargeCommand cmd, CancellationToken ct)
    {
        var already = await db.TenantBillingHistory.IgnoreQueryFilters()
            .AnyAsync(b => b.RazorpayPaymentId == cmd.RazorpayPaymentId, ct);
        if (already) return false;   // duplicate delivery — no-op

        // The charge must land on the PAYING tenant. On the anonymous webhook path the request's
        // tenant is the apex (tenant 1), and TenantBillingHistory is ITenantScoped — so without this
        // scope the auto-stamp would file another store's payment under tenant 1.
        using (tenant.BeginScope(cmd.TenantId))
        {
            db.TenantBillingHistory.Add(new TenantBillingHistory
            {
                TenantId = cmd.TenantId, Amount = cmd.Amount, Status = "Paid",
                RazorpayPaymentId = cmd.RazorpayPaymentId, BilledAt = DateTime.UtcNow,
                PeriodStart = cmd.PeriodStart, PeriodEnd = cmd.PeriodEnd, CreatedAt = DateTime.UtcNow,
            });

            var sub = await db.TenantSubscriptions.IgnoreQueryFilters()
                .Where(s => s.TenantId == cmd.TenantId)
                .OrderByDescending(s => s.TenantSubscriptionId).FirstOrDefaultAsync(ct);
            if (sub is not null)
            {
                sub.Status = Active;
                sub.CurrentPeriodStart = cmd.PeriodStart;
                sub.CurrentPeriodEnd = cmd.PeriodEnd;
                sub.GraceEndsAt = null;
                if (!string.IsNullOrEmpty(cmd.RazorpaySubscriptionId)) sub.RazorpaySubscriptionId = cmd.RazorpaySubscriptionId;
                sub.UpdatedAt = DateTime.UtcNow;
            }

            // Reactivate the store if it was suspended for non-payment.
            var store = await db.Tenants.FirstOrDefaultAsync(t => t.TenantId == cmd.TenantId, ct);
            if (store?.SuspendedAt is not null) { store.SuspendedAt = null; store.UpdatedAt = DateTime.UtcNow; }

            await db.SaveChangesAsync(ct);
        }
        return true;
    }

    /// <summary>
    /// Platform sweep (background). Trial/Active whose period has ended → PastDue + grace;
    /// PastDue past grace → Suspended (+ Tenant.SuspendedAt so the store 404s). Cross-tenant.
    /// </summary>
    public async Task<int> RunLifecycleSweepAsync(DateTime nowUtc, int graceDays, CancellationToken ct)
    {
        var changed = 0;

        var toGrace = await db.TenantSubscriptions.IgnoreQueryFilters()
            .Where(s => (s.Status == Trial || s.Status == Active)
                        && s.CurrentPeriodEnd != null && s.CurrentPeriodEnd < nowUtc && s.GraceEndsAt == null)
            .ToListAsync(ct);
        foreach (var s in toGrace)
        {
            s.Status = PastDue;
            s.GraceEndsAt = nowUtc.AddDays(graceDays);
            s.UpdatedAt = nowUtc;
            changed++;
        }

        var toSuspend = await db.TenantSubscriptions.IgnoreQueryFilters()
            .Where(s => s.Status == PastDue && s.GraceEndsAt != null && s.GraceEndsAt < nowUtc)
            .ToListAsync(ct);
        foreach (var s in toSuspend)
        {
            s.Status = Suspended;
            s.UpdatedAt = nowUtc;
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.TenantId == s.TenantId, ct);
            if (tenant is not null && tenant.SuspendedAt is null) { tenant.SuspendedAt = nowUtc; tenant.UpdatedAt = nowUtc; }
            changed++;
        }

        if (changed > 0) await db.SaveChangesAsync(ct);
        return changed;
    }

    private static SubscriptionDto ToDto(TenantSubscription s) => new(
        s.Status, s.PlanId, s.Plan?.Name ?? "", s.Plan?.Slug ?? "", s.Plan?.MonthlyPrice ?? 0,
        s.CurrentPeriodEnd, s.GraceEndsAt,
        IsActive: s.Status is Active or Trial,
        IsInTrial: s.Status == Trial);
}
