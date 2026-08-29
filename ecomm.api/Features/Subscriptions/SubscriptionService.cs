using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ecomm.api.Features.Subscriptions;

public sealed record SubscriptionDto(
    string Status, int PlanId, string PlanName, string PlanSlug, decimal MonthlyPrice,
    DateTime? CurrentPeriodEnd, DateTime? GraceEndsAt, bool IsActive, bool IsInTrial,
    string MandateStatus, DateTime? NextChargeAt, string? PaymentMethodSummary, bool CancelAtPeriodEnd);

/// <summary>Result of starting auto-pay setup. <see cref="AuthUrl"/> non-null (Razorpay) → redirect the
/// merchant there to authorize the mandate; <see cref="Active"/> true (Mock) → auto-pay is already on.</summary>
public sealed record AutoPaySetupDto(bool Active, string? AuthUrl, string MandateStatus, DateTime? NextChargeAt, string? PaymentMethodSummary);

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
    /// <summary>Hangfire recurring-job entry point (v4 Phase 0) — computes "now" and reads config at
    /// EXECUTION time. A Hangfire recurring-job expression only serializes its arguments once, at
    /// registration; passing DateTime.UtcNow directly there would freeze every future run at whatever
    /// moment the app last started, not the actual time each run fires.</summary>
    Task<int> RunScheduledLifecycleSweepAsync(CancellationToken ct);
    Task<CheckoutSessionDto> StartCheckoutAsync(int planId, CancellationToken ct);            // pay one cycle
    Task<SubscriptionDto> ConfirmCheckoutAsync(ConfirmCheckoutCommand cmd, CancellationToken ct);
    // P2 — recurring auto-debit
    Task<AutoPaySetupDto> SetupAutoPayAsync(int planId, CancellationToken ct);
    Task CancelAutoPayAsync(CancellationToken ct);
    Task HandleSubscriptionEventAsync(string eventType, long tenantId, string? paymentId, decimal amount, string? subscriptionId, CancellationToken ct);
}

public sealed class SubscriptionService(
    EcommerceDbContext db,
    ecomm.api.Features.Payments.PlatformPaymentGatewayFactory gateways,
    ecomm.api.Common.Tenancy.ICurrentTenantService tenant,
    ecomm.api.Features.Notifications.IEmailSender email,
    IPlatformInvoiceService invoices,
    Microsoft.Extensions.Options.IOptions<ecomm.api.Common.Tenancy.TenancyOptions> tenancy,
    ILogger<SubscriptionService> log,
    IConfiguration configuration) : ISubscriptionService
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
            // Anchor the trial's period end to the tenant's TrialEndsAt so the lifecycle sweep can expire
            // it (the sweep keys off CurrentPeriodEnd). Fallback to a default trial length if unset.
            var trialEnds = await db.Tenants.Where(t => t.TenantId == db.CurrentTenantId)
                .Select(t => t.TrialEndsAt).FirstOrDefaultAsync(ct)
                ?? DateTime.UtcNow.AddDays(configuration.GetValue("Billing:TrialDays", 14));
            sub = new TenantSubscription { PlanId = plan.PlanId, Status = Trial, CurrentPeriodEnd = trialEnds, CreatedAt = DateTime.UtcNow };
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
    /// <summary>
    /// What this tenant pays for the NEXT cycle of a plan. Introductory pricing applies to the first
    /// <c>IntroMonths</c> paid cycles — measured by how many charges the tenant already has — after
    /// which it reverts to the standard monthly price.
    /// </summary>
    private async Task<decimal> EffectivePriceAsync(Plan plan, CancellationToken ct)
    {
        if (plan.IntroPriceInr is not { } intro || plan.IntroMonths is not { } months || months <= 0)
            return plan.MonthlyPrice;

        var paidCycles = await db.TenantBillingHistory.CountAsync(b => b.Status == "Paid", ct);   // tenant-scoped
        if (paidCycles >= months) return plan.MonthlyPrice;                                      // intro used up

        // Campaign deadline closes the offer to NEW joiners only. A merchant already part-way
        // through the offer keeps the price they were promised for the rest of their cycles.
        if (paidCycles == 0 && plan.IntroEndsAt is { } endsAt && DateTime.UtcNow > endsAt)
            return plan.MonthlyPrice;

        return intro;
    }

    public async Task<CheckoutSessionDto> StartCheckoutAsync(int planId, CancellationToken ct)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive, ct)
                   ?? throw new AppException("Plan not found.", StatusCodes.Status404NotFound);
        var price = await EffectivePriceAsync(plan, ct);
        if (price <= 0) throw new AppException("That plan is free right now — just select it.", StatusCodes.Status400BadRequest);

        var gateway = gateways.Create();
        var order = await gateway.CreateOrderAsync(db.CurrentTenantId, price, "INR", $"sub-{db.CurrentTenantId}-{planId}", ct);
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

        // Re-derive the price server-side (same rule as StartCheckout) — never trust the client.
        var price = await EffectivePriceAsync(plan, ct);
        await SelectPlanAsync(cmd.PlanId, ct);   // point the subscription at the plan being paid for
        var now = DateTime.UtcNow;
        await RecordChargeAsync(new RecordChargeCommand(db.CurrentTenantId, price, cmd.PaymentId, null, now, now.AddMonths(1)), ct);
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
            var history = new TenantBillingHistory
            {
                TenantId = cmd.TenantId, Amount = cmd.Amount, Status = "Paid",
                RazorpayPaymentId = cmd.RazorpayPaymentId, BilledAt = DateTime.UtcNow,
                PeriodStart = cmd.PeriodStart, PeriodEnd = cmd.PeriodEnd, CreatedAt = DateTime.UtcNow,
            };
            db.TenantBillingHistory.Add(history);

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

            // Issue the GST tax invoice for this charge (best-effort — the charge itself must not fail on it).
            try { await invoices.GenerateForChargeAsync(cmd.TenantId, history.TenantBillingHistoryId, cmd.Amount, ct); }
            catch (Exception ex) { log.LogWarning(ex, "Platform invoice generation failed for charge {Charge}.", history.TenantBillingHistoryId); }
        }
        return true;
    }

    public Task<int> RunScheduledLifecycleSweepAsync(CancellationToken ct) =>
        RunLifecycleSweepAsync(DateTime.UtcNow, configuration.GetValue("Billing:GraceDays", 3), ct);

    /// <summary>
    /// Platform sweep (background). Trial/Active whose period has ended → PastDue + grace;
    /// PastDue past grace → Suspended (+ Tenant.SuspendedAt so the store 404s). Cross-tenant.
    /// </summary>
    public async Task<int> RunLifecycleSweepAsync(DateTime nowUtc, int graceDays, CancellationToken ct)
    {
        var changed = 0;

        changed += await SendTrialRemindersAsync(nowUtc, ct);

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

    /// <summary>
    /// Emails a trial's admin as it approaches expiry — one reminder each at 7 / 3 / 1 days before
    /// <c>CurrentPeriodEnd</c> (tracked by <c>TrialReminderStage</c> so none repeats). Best-effort: a send
    /// failure is logged and retried next sweep (the stage is only advanced on success). Returns the number
    /// of reminders sent (folded into the sweep's change count so it saves once).
    /// </summary>
    private async Task<int> SendTrialRemindersAsync(DateTime nowUtc, CancellationToken ct)
    {
        var endingSoon = await db.TenantSubscriptions.IgnoreQueryFilters()
            .Where(s => s.Status == Trial && s.CurrentPeriodEnd != null
                        && s.CurrentPeriodEnd > nowUtc && s.CurrentPeriodEnd <= nowUtc.AddDays(7))
            .ToListAsync(ct);
        if (endingSoon.Count == 0) return 0;

        var tenantIds = endingSoon.Select(s => s.TenantId).Distinct().ToList();
        var contactByTenant = (await (
                from u in db.Users.IgnoreQueryFilters()
                join ur in db.UserRoles on u.UserId equals ur.UserId
                join r in db.Roles on ur.RoleId equals r.RoleId
                where tenantIds.Contains(u.TenantId) && !u.IsDeleted && r.NormalizedName == "ADMIN" && u.Email != null
                select new { u.TenantId, u.Email, u.FullName }).ToListAsync(ct))
            .GroupBy(c => c.TenantId).ToDictionary(g => g.Key, g => g.First());
        var stores = (await db.Tenants.IgnoreQueryFilters().Where(t => tenantIds.Contains(t.TenantId))
                .Select(t => new { t.TenantId, t.Name, t.Slug, t.CustomDomain, t.CustomDomainVerified }).ToListAsync(ct))
            .ToDictionary(x => x.TenantId);
        var baseDomain = (tenancy.Value.BaseDomain ?? "").Trim();

        var sent = 0;
        foreach (var s in endingSoon)
        {
            var daysLeft = Math.Max(1, (int)Math.Ceiling((s.CurrentPeriodEnd!.Value - nowUtc).TotalDays));
            var stage = daysLeft <= 1 ? 1 : daysLeft <= 3 ? 3 : 7;
            if (s.TrialReminderStage is { } prev && stage >= prev) continue;   // already reminded at this-or-more-urgent stage
            if (!contactByTenant.TryGetValue(s.TenantId, out var c) || string.IsNullOrWhiteSpace(c.Email)) continue;
            if (!stores.TryGetValue(s.TenantId, out var store)) continue;

            var host = store.CustomDomainVerified && !string.IsNullOrEmpty(store.CustomDomain) ? store.CustomDomain
                     : !string.IsNullOrEmpty(baseDomain) && !string.IsNullOrEmpty(store.Slug) ? $"{store.Slug}.{baseDomain}" : null;
            var billingUrl = host is null ? null : $"https://{host}/admin/billing";
            var greeting = string.IsNullOrWhiteSpace(c.FullName) ? "Hi" : $"Hi {System.Net.WebUtility.HtmlEncode(c.FullName)}";
            var subject = daysLeft <= 1 ? $"Your {store.Name} trial ends tomorrow" : $"Your {store.Name} trial ends in {daysLeft} days";
            var body = $"<p>{greeting},</p>"
                     + $"<p>Your free trial for <strong>{System.Net.WebUtility.HtmlEncode(store.Name)}</strong> ends in {daysLeft} day(s). "
                     + "To keep your store online, choose a plan and pay before it ends.</p>"
                     + (billingUrl is null ? "" : $"<p><a href=\"{billingUrl}\">Go to billing &rarr;</a></p>");
            try
            {
                await email.SendAsync(c.Email!, subject, body, ct);
                s.TrialReminderStage = stage;
                s.UpdatedAt = nowUtc;
                sent++;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Trial reminder email to {Email} (tenant {Tenant}) failed.", c.Email, s.TenantId);
            }
        }
        return sent;
    }

    private static SubscriptionDto ToDto(TenantSubscription s) => new(
        s.Status, s.PlanId, s.Plan?.Name ?? "", s.Plan?.Slug ?? "", s.Plan?.MonthlyPrice ?? 0,
        s.CurrentPeriodEnd, s.GraceEndsAt,
        IsActive: s.Status is Active or Trial,
        IsInTrial: s.Status == Trial,
        s.MandateStatus, s.NextChargeAt, s.PaymentMethodSummary, s.CancelAtPeriodEnd);

    // ===== P2: recurring auto-debit (Razorpay Subscriptions) =====

    /// <summary>
    /// Start auto-pay for a plan. Ensures a Razorpay Plan (cached on <see cref="Plan.RazorpayPlanId"/>),
    /// creates a subscription/mandate, and stores its state. Mock activates immediately (and records the
    /// first charge so the plan goes live); Razorpay returns a hosted auth URL — the merchant authorizes the
    /// e-mandate there, and activation/charges then arrive via <c>subscription.*</c> webhooks.
    /// </summary>
    public async Task<AutoPaySetupDto> SetupAutoPayAsync(int planId, CancellationToken ct)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive, ct)
                   ?? throw new AppException("Plan not found.", StatusCodes.Status404NotFound);
        var price = await EffectivePriceAsync(plan, ct);
        if (price <= 0) throw new AppException("That plan is free — just select it, no auto-pay needed.", StatusCodes.Status400BadRequest);

        var gateway = gateways.CreateRecurring();
        if (string.IsNullOrEmpty(plan.RazorpayPlanId))
        {
            plan.RazorpayPlanId = await gateway.EnsurePlanAsync(plan, price, ct);
            await db.SaveChangesAsync(ct);
        }

        await SelectPlanAsync(planId, ct);   // ensure a subscription row pointing at this plan
        var sub = await db.TenantSubscriptions.OrderByDescending(x => x.TenantSubscriptionId).FirstAsync(ct);

        var setup = await gateway.CreateSubscriptionAsync(db.CurrentTenantId, planId, plan.RazorpayPlanId!, notifyEmail: null, ct);
        sub.RazorpaySubscriptionId = setup.SubscriptionId;
        sub.RazorpayCustomerId = setup.CustomerId;
        sub.PaymentMethodSummary = setup.PaymentMethodSummary;
        sub.NextChargeAt = setup.NextChargeAt;
        sub.CancelAtPeriodEnd = false;
        sub.MandateStatus = setup.Active ? "active" : "pending";
        sub.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Mock: no webhook will arrive, so record the first cycle now to make the flow complete end-to-end.
        if (setup.Active)
        {
            var now = DateTime.UtcNow;
            await RecordChargeAsync(new RecordChargeCommand(db.CurrentTenantId, price, $"mockpay_{setup.SubscriptionId}", setup.SubscriptionId, now, now.AddMonths(1)), ct);
        }

        return new AutoPaySetupDto(setup.Active, setup.AuthUrl, sub.MandateStatus, sub.NextChargeAt, sub.PaymentMethodSummary);
    }

    /// <summary>Cancel auto-pay at the end of the current cycle. Access continues until then.</summary>
    public async Task CancelAutoPayAsync(CancellationToken ct)
    {
        var sub = await db.TenantSubscriptions.OrderByDescending(x => x.TenantSubscriptionId).FirstOrDefaultAsync(ct);
        if (sub is null || string.IsNullOrEmpty(sub.RazorpaySubscriptionId)) return;
        try { await gateways.CreateRecurring().CancelSubscriptionAsync(sub.RazorpaySubscriptionId!, atCycleEnd: true, ct); }
        catch (Exception ex) { log.LogWarning(ex, "Auto-pay cancel at gateway failed for {Sub}; marking locally.", sub.RazorpaySubscriptionId); }
        sub.CancelAtPeriodEnd = true;
        sub.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Apply a Razorpay <c>subscription.*</c> webhook to the tenant's subscription. Cross-tenant (webhook
    /// path), so it re-establishes the tenant scope. Charges reuse the idempotent <see cref="RecordChargeAsync"/>.
    /// </summary>
    public async Task HandleSubscriptionEventAsync(string eventType, long tenantId, string? paymentId, decimal amount, string? subscriptionId, CancellationToken ct)
    {
        if (eventType is "subscription.charged" && !string.IsNullOrWhiteSpace(paymentId))
        {
            var now = DateTime.UtcNow;
            await RecordChargeAsync(new RecordChargeCommand(tenantId, amount, paymentId!, subscriptionId, now, now.AddMonths(1)), ct);
            using (tenant.BeginScope(tenantId))
            {
                var sub = await db.TenantSubscriptions.IgnoreQueryFilters().Where(s => s.TenantId == tenantId)
                    .OrderByDescending(s => s.TenantSubscriptionId).FirstOrDefaultAsync(ct);
                if (sub is not null) { sub.MandateStatus = "active"; sub.NextChargeAt = now.AddMonths(1); await db.SaveChangesAsync(ct); }
            }
            return;
        }

        using (tenant.BeginScope(tenantId))
        {
            var sub = await db.TenantSubscriptions.IgnoreQueryFilters().Where(s => s.TenantId == tenantId)
                .OrderByDescending(s => s.TenantSubscriptionId).FirstOrDefaultAsync(ct);
            if (sub is null) return;
            var nowUtc = DateTime.UtcNow;
            switch (eventType)
            {
                case "subscription.pending":   // a charge failed; Razorpay will retry
                    if (sub.Status is Active or Trial) { sub.Status = PastDue; sub.GraceEndsAt = nowUtc.AddDays(configuration.GetValue("Billing:GraceDays", 3)); }
                    break;
                case "subscription.halted":    // retries exhausted
                    sub.Status = Suspended;
                    var t1 = await db.Tenants.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
                    if (t1 is { SuspendedAt: null }) { t1.SuspendedAt = nowUtc; t1.UpdatedAt = nowUtc; }
                    break;
                case "subscription.cancelled":
                    sub.MandateStatus = "cancelled";
                    break;
                case "subscription.activated":
                    sub.MandateStatus = "active";
                    break;
            }
            sub.UpdatedAt = nowUtc;
            await db.SaveChangesAsync(ct);
        }
    }
}
