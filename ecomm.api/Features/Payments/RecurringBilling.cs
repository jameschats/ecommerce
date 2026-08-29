using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Entities;

namespace ecomm.api.Features.Payments;

/// <summary>Result of setting up auto-pay. <see cref="AuthUrl"/> is the hosted mandate-authorization page
/// (Razorpay); <see cref="Active"/> is true only when the mandate is already usable (the Mock gateway).</summary>
public sealed record RecurringSetup(
    string SubscriptionId, string? CustomerId, string? AuthUrl, bool Active,
    DateTime? NextChargeAt, string? PaymentMethodSummary);

/// <summary>
/// Recurring auto-debit for platform subscriptions (merchant → platform), separate from the one-time
/// <see cref="IPaymentGateway"/>. Real implementation is Razorpay Subscriptions (RBI e-mandate / UPI
/// Autopay); Mock makes the whole flow work in dev/demo without a live account.
/// </summary>
public interface IRecurringBillingGateway
{
    string Name { get; }
    bool IsMock { get; }
    /// <summary>Ensure a Razorpay Plan exists for this platform plan+price; returns the razorpay plan id (cached by the caller).</summary>
    Task<string> EnsurePlanAsync(Plan plan, decimal amount, CancellationToken ct = default);
    /// <summary>Create a subscription (mandate). Returns the hosted auth URL (Razorpay) or an already-active mandate (Mock).</summary>
    Task<RecurringSetup> CreateSubscriptionAsync(long tenantId, int planId, string razorpayPlanId, string? notifyEmail, CancellationToken ct = default);
    Task CancelSubscriptionAsync(string subscriptionId, bool atCycleEnd, CancellationToken ct = default);
}

/// <summary>Dev/demo recurring gateway: the mandate is "authorized" immediately so the flow completes
/// end-to-end without a real Razorpay account. Charges are then driven by the caller / a simulated webhook.</summary>
public sealed class MockRecurringGateway : IRecurringBillingGateway
{
    public string Name => "Mock";
    public bool IsMock => true;

    public Task<string> EnsurePlanAsync(Plan plan, decimal amount, CancellationToken ct = default)
        => Task.FromResult($"mockplan_{plan.PlanId}");

    public Task<RecurringSetup> CreateSubscriptionAsync(long tenantId, int planId, string razorpayPlanId, string? notifyEmail, CancellationToken ct = default)
        => Task.FromResult(new RecurringSetup(
            SubscriptionId: $"mocksub_{tenantId}_{planId}_{Guid.NewGuid():N}".Substring(0, 40),
            CustomerId: $"mockcust_{tenantId}",
            AuthUrl: null, Active: true,
            NextChargeAt: DateTime.UtcNow.AddMonths(1),
            PaymentMethodSummary: "Mock autopay"));

    public Task CancelSubscriptionAsync(string subscriptionId, bool atCycleEnd, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// Real Razorpay Subscriptions gateway. Creates a Razorpay Plan + Subscription; the merchant authorizes the
/// e-mandate at the returned short_url, after which Razorpay auto-charges each cycle and fires
/// <c>subscription.*</c> webhooks. <b>Unverified against a live account</b> — enable once the platform's
/// Razorpay account has Subscriptions turned on and real keys are configured.
/// </summary>
public sealed class RazorpaySubscriptionGateway : IRecurringBillingGateway
{
    private readonly HttpClient _http;
    private const int TotalCount = 120;   // Razorpay requires a bound; 120 monthly cycles = 10 years

    public RazorpaySubscriptionGateway(HttpClient http, string keyId, string keySecret)
    {
        _http = http;
        _http.BaseAddress ??= new Uri("https://api.razorpay.com/");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
    }

    public string Name => "Razorpay";
    public bool IsMock => false;

    public async Task<string> EnsurePlanAsync(Plan plan, decimal amount, CancellationToken ct = default)
    {
        var paise = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
        var body = JsonSerializer.Serialize(new
        {
            period = "monthly",
            interval = 1,
            item = new { name = plan.Name, amount = paise, currency = "INR" },
        });
        using var resp = await _http.PostAsync("v1/plans", new StringContent(body, Encoding.UTF8, "application/json"), ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new AppException($"Razorpay plan creation failed: {json}", 502);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<RecurringSetup> CreateSubscriptionAsync(long tenantId, int planId, string razorpayPlanId, string? notifyEmail, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            plan_id = razorpayPlanId,
            total_count = TotalCount,
            quantity = 1,
            customer_notify = 1,
            notes = new { tenantId = tenantId.ToString(), planId = planId.ToString() },
        });
        using var resp = await _http.PostAsync("v1/subscriptions", new StringContent(body, Encoding.UTF8, "application/json"), ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new AppException($"Razorpay subscription creation failed: {json}", 502);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var id = root.GetProperty("id").GetString()!;
        var shortUrl = root.TryGetProperty("short_url", out var su) ? su.GetString() : null;
        // Not active until the merchant authorizes the mandate at short_url; activation arrives by webhook.
        return new RecurringSetup(id, null, shortUrl, Active: false, NextChargeAt: null, PaymentMethodSummary: null);
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, bool atCycleEnd, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { cancel_at_cycle_end = atCycleEnd ? 1 : 0 });
        using var resp = await _http.PostAsync($"v1/subscriptions/{subscriptionId}/cancel",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        if (!resp.IsSuccessStatusCode)
        {
            var json = await resp.Content.ReadAsStringAsync(ct);
            throw new AppException($"Razorpay subscription cancel failed: {json}", 502);
        }
    }
}
