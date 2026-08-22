using System.Net;
using System.Security.Cryptography;
using System.Text;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.PublicApi;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ecomm.tests;

public class WebhookServiceTests
{
    private static (EcommerceDbContext db, WebhookSubscriptionService subs, WebhookDispatchService dispatch, FakeHandler handler, RecordingScheduler scheduler) Setup()
    {
        var db = TestDb.New(tenantId: 1);
        var subs = new WebhookSubscriptionService(db, DataProtectionProvider.Create("ecomm-tests"));
        var handler = new FakeHandler();
        var factory = new FakeHttpClientFactory(handler);
        var scheduler = new RecordingScheduler();
        var dispatch = new WebhookDispatchService(db, subs, factory, scheduler, NullLogger<WebhookDispatchService>.Instance);
        return (db, subs, dispatch, handler, scheduler);
    }

    // ---------------- Subscription CRUD ----------------

    [Fact]
    public async Task Creating_a_subscription_returns_the_secret_exactly_once_and_it_is_encrypted_at_rest()
    {
        var (db, subs, _, _, _) = Setup();
        using var _db = db;

        var created = await subs.CreateAsync(new CreateWebhookSubscriptionRequest("https://example.com/hook", ["order.created"]), userId: 5);

        Assert.NotEmpty(created.Secret);
        var stored = db.WebhookSubscriptions.Single().EncryptedSecret;
        Assert.NotEqual(created.Secret, stored);   // never stored in the clear
        Assert.Equal(created.Secret, await subs.GetSecretAsync(created.Id));   // but reversible for signing
    }

    [Fact]
    public async Task An_invalid_url_is_rejected()
    {
        var (db, subs, _, _, _) = Setup();
        using var _db = db;

        await Assert.ThrowsAsync<AppException>(() => subs.CreateAsync(new CreateWebhookSubscriptionRequest("not-a-url", ["order.created"]), 5));
    }

    [Fact]
    public async Task An_unknown_event_is_rejected()
    {
        var (db, subs, _, _, _) = Setup();
        using var _db = db;

        await Assert.ThrowsAsync<AppException>(() => subs.CreateAsync(new CreateWebhookSubscriptionRequest("https://example.com", ["some.made.up.event"]), 5));
    }

    // ---------------- Dispatch ----------------

    [Fact]
    public async Task Dispatch_delivers_to_a_matching_active_subscription_with_a_valid_HMAC_signature()
    {
        var (db, subs, dispatch, handler, _) = Setup();
        using var _db = db;
        var sub = await subs.CreateAsync(new CreateWebhookSubscriptionRequest("https://example.com/hook", ["order.created"]), 5);
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK);

        await dispatch.DispatchAsync("order.created", new { orderId = 42 });

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("order.created", handler.LastRequest!.Headers.GetValues("X-WavCommerce-Event").Single());
        var signature = handler.LastRequest.Headers.GetValues("X-WavCommerce-Signature").Single();
        var expected = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(sub.Secret), Encoding.UTF8.GetBytes(handler.LastBody!))).ToLowerInvariant();
        Assert.Equal(expected, signature);

        var delivery = db.WebhookDeliveries.Single();
        Assert.Equal("Delivered", delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.NotNull(delivery.DeliveredAt);
    }

    [Fact]
    public async Task Dispatch_never_calls_a_subscription_for_an_event_it_did_not_subscribe_to()
    {
        var (db, subs, dispatch, handler, _) = Setup();
        using var _db = db;
        await subs.CreateAsync(new CreateWebhookSubscriptionRequest("https://example.com/hook", ["product.updated"]), 5);
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK);

        await dispatch.DispatchAsync("order.created", new { orderId = 1 });

        Assert.Null(handler.LastRequest);
        Assert.Empty(db.WebhookDeliveries);
    }

    [Fact]
    public async Task Dispatch_never_calls_an_inactive_subscription()
    {
        var (db, subs, dispatch, handler, _) = Setup();
        using var _db = db;
        var sub = await subs.CreateAsync(new CreateWebhookSubscriptionRequest("https://example.com/hook", ["order.created"]), 5);
        await subs.SetActiveAsync(sub.Id, false);

        await dispatch.DispatchAsync("order.created", new { orderId = 1 });

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task A_non_success_response_schedules_the_first_retry_with_the_correct_delay()
    {
        var (db, subs, dispatch, handler, scheduler) = Setup();
        using var _db = db;
        await subs.CreateAsync(new CreateWebhookSubscriptionRequest("https://example.com/hook", ["order.created"]), 5);
        handler.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        await dispatch.DispatchAsync("order.created", new { });

        var delivery = db.WebhookDeliveries.Single();
        Assert.Equal("Pending", delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Equal(500, delivery.LastStatusCode);
        Assert.Single(scheduler.Scheduled);
        Assert.Equal(TimeSpan.FromMinutes(1), scheduler.Scheduled[0].Delay);
    }

    [Fact]
    public async Task A_thrown_exception_during_delivery_is_recorded_and_still_schedules_a_retry()
    {
        var (db, subs, dispatch, handler, scheduler) = Setup();
        using var _db = db;
        await subs.CreateAsync(new CreateWebhookSubscriptionRequest("https://example.com/hook", ["order.created"]), 5);
        handler.ThrowOnSend = true;

        await dispatch.DispatchAsync("order.created", new { });

        var delivery = db.WebhookDeliveries.Single();
        Assert.Equal("Pending", delivery.Status);
        Assert.NotNull(delivery.LastError);
        Assert.Single(scheduler.Scheduled);
    }

    [Fact]
    public async Task After_exhausting_every_retry_delay_the_delivery_is_marked_Exhausted_not_retried_again()
    {
        var (db, subs, dispatch, handler, scheduler) = Setup();
        using var _db = db;
        await subs.CreateAsync(new CreateWebhookSubscriptionRequest("https://example.com/hook", ["order.created"]), 5);
        handler.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        await dispatch.DispatchAsync("order.created", new { });   // attempt 1
        var deliveryId = db.WebhookDeliveries.Single().WebhookDeliveryId;
        for (var i = 0; i < 5; i++)   // 5 more attempts = the full RetryDelays schedule
            await dispatch.RetryDeliveryAsync(deliveryId);

        var delivery = db.WebhookDeliveries.Single();
        Assert.Equal("Exhausted", delivery.Status);
        Assert.Equal(6, delivery.AttemptCount);
        Assert.Equal(5, scheduler.Scheduled.Count);   // one retry scheduled per failed attempt, none after Exhausted
    }

    [Fact]
    public async Task Retrying_an_already_delivered_delivery_is_a_no_op()
    {
        var (db, subs, dispatch, handler, scheduler) = Setup();
        using var _db = db;
        await subs.CreateAsync(new CreateWebhookSubscriptionRequest("https://example.com/hook", ["order.created"]), 5);
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK);
        await dispatch.DispatchAsync("order.created", new { });
        var deliveryId = db.WebhookDeliveries.Single().WebhookDeliveryId;

        await dispatch.RetryDeliveryAsync(deliveryId);

        Assert.Equal(1, db.WebhookDeliveries.Single().AttemptCount);   // unchanged — RetryDeliveryAsync returned early
    }

    [Fact]
    public async Task Retrying_a_delivery_whose_subscription_was_deactivated_since_scheduling_does_not_deliver()
    {
        var (db, subs, dispatch, handler, _) = Setup();
        using var _db = db;
        var sub = await subs.CreateAsync(new CreateWebhookSubscriptionRequest("https://example.com/hook", ["order.created"]), 5);
        handler.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        await dispatch.DispatchAsync("order.created", new { });
        var deliveryId = db.WebhookDeliveries.Single().WebhookDeliveryId;
        await subs.SetActiveAsync(sub.Id, false);
        handler.LastRequest = null;

        await dispatch.RetryDeliveryAsync(deliveryId);

        Assert.Null(handler.LastRequest);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;
        public string? LastBody;
        public bool ThrowOnSend;
        public HttpResponseMessage Response = new(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (ThrowOnSend) throw new HttpRequestException("simulated network failure");
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return Response;
        }
    }

    private sealed class FakeHttpClientFactory(FakeHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private sealed class RecordingScheduler : IWebhookRetryScheduler
    {
        public List<(long DeliveryId, TimeSpan Delay)> Scheduled { get; } = new();
        public void ScheduleRetry(long deliveryId, TimeSpan delay) => Scheduled.Add((deliveryId, delay));
    }
}
