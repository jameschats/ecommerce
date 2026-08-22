using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.PublicApi;

public interface IWebhookDispatchService
{
    /// <summary>Fires from a real platform event — finds matching active subscriptions for the
    /// current tenant, creates one delivery row per subscription (the full audit trail, regardless
    /// of outcome), and attempts each immediately.</summary>
    Task DispatchAsync(string eventType, object payload, CancellationToken ct = default);

    /// <summary>Hangfire-invoked only — retries one delivery by id.</summary>
    Task RetryDeliveryAsync(long deliveryId, CancellationToken ct = default);
}

/// <summary>Wraps Hangfire's static <c>BackgroundJob</c> API purely for testability — same shape as
/// <see cref="ecomm.api.Features.Notifications.IBackgroundJobScheduler"/>.</summary>
public interface IWebhookRetryScheduler
{
    void ScheduleRetry(long deliveryId, TimeSpan delay);
}

public sealed class HangfireWebhookRetryScheduler : IWebhookRetryScheduler
{
    public void ScheduleRetry(long deliveryId, TimeSpan delay)
        => BackgroundJob.Schedule<IWebhookDispatchService>(svc => svc.RetryDeliveryAsync(deliveryId, CancellationToken.None), delay);
}

/// <summary>
/// Delivers webhooks (v4 Phase 6 Track A) with HMAC-SHA256 signed payloads (Shopify/Stripe-style
/// <c>X-WavCommerce-Signature: sha256=...</c>) and a bounded, backing-off retry schedule — 5 more
/// attempts after the first, then <c>Exhausted</c> (distinguishable in the audit log from still
/// mid-retry). Every attempt persists as its own state on the one <see cref="WebhookDelivery"/> row
/// (attempt count, last status code, last error), same "no black-box" posture as the rest of this
/// v4 build's audit trails.
/// </summary>
public sealed class WebhookDispatchService(
    EcommerceDbContext db, IWebhookSubscriptionService subscriptions, IHttpClientFactory httpFactory,
    IWebhookRetryScheduler scheduler, ILogger<WebhookDispatchService> logger) : IWebhookDispatchService
{
    private long Tenant => db.CurrentTenantId;

    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6)];

    public async Task DispatchAsync(string eventType, object payload, CancellationToken ct = default)
    {
        var subs = await db.WebhookSubscriptions.Where(s => s.IsActive).ToListAsync(ct);
        var matching = subs.Where(s => SplitEvents(s.Events).Contains(eventType, StringComparer.OrdinalIgnoreCase)).ToList();
        if (matching.Count == 0) return;

        var payloadJson = JsonSerializer.Serialize(payload);
        foreach (var sub in matching)
        {
            var delivery = new WebhookDelivery
            {
                TenantId = Tenant, WebhookSubscriptionId = sub.WebhookSubscriptionId, EventType = eventType,
                Payload = payloadJson, Status = "Pending", CreatedAt = DateTime.UtcNow,
            };
            db.WebhookDeliveries.Add(delivery);
            await db.SaveChangesAsync(ct);   // needs its own id before the first attempt/scheduled retry
            await AttemptAsync(delivery, sub, ct);
        }
    }

    public async Task RetryDeliveryAsync(long deliveryId, CancellationToken ct = default)
    {
        var delivery = await db.WebhookDeliveries.Include(d => d.Subscription).FirstOrDefaultAsync(d => d.WebhookDeliveryId == deliveryId, ct);
        if (delivery is null || delivery.Status is "Delivered" or "Exhausted") return;
        if (delivery.Subscription is not { IsActive: true } sub) return;   // subscription deleted/deactivated since scheduling
        await AttemptAsync(delivery, sub, ct);
    }

    private async Task AttemptAsync(WebhookDelivery delivery, WebhookSubscription sub, CancellationToken ct)
    {
        delivery.AttemptCount++;
        delivery.LastAttemptAt = DateTime.UtcNow;

        try
        {
            var secret = await subscriptions.GetSecretAsync(sub.WebhookSubscriptionId, ct);
            var http = httpFactory.CreateClient("webhook");
            using var req = new HttpRequestMessage(HttpMethod.Post, sub.Url)
            {
                Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json"),
            };
            req.Headers.TryAddWithoutValidation("X-WavCommerce-Event", delivery.EventType);
            req.Headers.TryAddWithoutValidation("X-WavCommerce-Delivery", delivery.WebhookDeliveryId.ToString());
            if (secret is not null)
            {
                var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(delivery.Payload))).ToLowerInvariant();
                req.Headers.TryAddWithoutValidation("X-WavCommerce-Signature", $"sha256={signature}");
            }

            using var res = await http.SendAsync(req, ct);
            delivery.LastStatusCode = (int)res.StatusCode;

            if (res.IsSuccessStatusCode)
            {
                delivery.Status = "Delivered";
                delivery.DeliveredAt = DateTime.UtcNow;
            }
            else
            {
                delivery.LastError = $"HTTP {(int)res.StatusCode}";
                ScheduleOrExhaust(delivery);
            }
        }
        catch (Exception ex)
        {
            delivery.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            logger.LogWarning(ex, "Webhook delivery {Id} attempt {Attempt} failed.", delivery.WebhookDeliveryId, delivery.AttemptCount);
            ScheduleOrExhaust(delivery);
        }

        await db.SaveChangesAsync(ct);
    }

    private void ScheduleOrExhaust(WebhookDelivery delivery)
    {
        var attemptIndex = delivery.AttemptCount - 1;   // 0-based into RetryDelays
        if (attemptIndex < RetryDelays.Length)
        {
            delivery.Status = "Pending";
            scheduler.ScheduleRetry(delivery.WebhookDeliveryId, RetryDelays[attemptIndex]);
        }
        else
        {
            delivery.Status = "Exhausted";
        }
    }

    private static IReadOnlyList<string> SplitEvents(string events) =>
        events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
