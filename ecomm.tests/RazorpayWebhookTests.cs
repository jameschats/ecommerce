using System.Security.Cryptography;
using System.Text;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Payments;
using ecomm.api.Features.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

public class RazorpayWebhookTests
{
    private const string Secret = "whsec_test_secret";

    private sealed class StubHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new NotImplementedException();
    }

    private sealed class NoopEmailSender : ecomm.api.Features.Notifications.IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default, string? fromName = null, string? replyTo = null) => Task.CompletedTask;
    }

    private static string Sign(string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    // Razorpay's order.paid shape: the order carries our receipt, the payment carries the id.
    // (Braces are spaced apart so they don't collide with raw-string interpolation.)
    private static string OrderPaidBody(string receipt, long paise, string paymentId) => $$"""
    {
      "event": "order.paid",
      "payload": {
        "order": { "entity": { "id": "order_abc", "receipt": "{{receipt}}", "amount": {{paise}}, "currency": "INR" } },
        "payment": { "entity": { "id": "{{paymentId}}", "amount": {{paise}} } }
      }
    }
    """;

    private static (RazorpayWebhookService hook, ecomm.api.Data.Context.EcommerceDbContext db) Build()
    {
        // Context tenant = 1 (the apex, exactly like the anonymous webhook request).
        var tenant = new FixedTenant(1);
        var db = TestDb.ForDatabase(Guid.NewGuid().ToString(), tenant);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Billing:WebhookSecret"] = Secret }).Build();
        var subs = new SubscriptionService(db,
            new PlatformPaymentGatewayFactory(new StubHttpFactory(), Options.Create(new PaymentOptions()), db,
                Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create("ecomm.tests")), tenant,
            new NoopEmailSender(), Options.Create(new ecomm.api.Common.Tenancy.TenancyOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SubscriptionService>.Instance, config);

        db.Tenants.Add(new Tenant { TenantId = 3, Name = "Acme", Code = "acme", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Plans.Add(new Plan { PlanId = 1, Name = "Pro", Slug = "pro", MonthlyPrice = 999, IsActive = true });
        db.SaveChanges();
        using (tenant.BeginScope(3))
        {
            db.TenantSubscriptions.Add(new TenantSubscription { TenantId = 3, PlanId = 1, Status = "Trial", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }
        return (new RazorpayWebhookService(subs, config, NullLogger<RazorpayWebhookService>.Instance), db);
    }

    [Fact]
    public void Signature_must_match_the_raw_body()
    {
        var (hook, db) = Build();
        using (db)
        {
            var body = OrderPaidBody("sub-3-1", 99900, "pay_1");
            Assert.True(hook.VerifySignature(body, Sign(body)));
            Assert.False(hook.VerifySignature(body + " ", Sign(body)));   // body tampered
            Assert.False(hook.VerifySignature(body, "deadbeef"));         // wrong signature
            Assert.False(hook.VerifySignature(body, null));               // missing
        }
    }

    [Fact]
    public async Task Order_paid_lands_the_charge_on_the_paying_tenant_and_is_idempotent()
    {
        var (hook, db) = Build();
        using (db)
        {
            var body = OrderPaidBody("sub-3-1", 99900, "pay_abc");

            var outcome = await hook.HandleAsync(body, default);
            Assert.True(outcome.Handled);

            var charge = await db.TenantBillingHistory.IgnoreQueryFilters().SingleAsync();
            Assert.Equal(3, charge.TenantId);          // NOT the apex tenant the request ran under
            Assert.Equal(999m, charge.Amount);         // paise -> rupees
            Assert.Equal("Paid", charge.Status);

            var sub = await db.TenantSubscriptions.IgnoreQueryFilters().SingleAsync(s => s.TenantId == 3);
            Assert.Equal(SubscriptionService.Active, sub.Status);

            var again = await hook.HandleAsync(body, default);            // Razorpay retry / confirm race
            Assert.False(again.Handled);
            Assert.Equal(1, await db.TenantBillingHistory.IgnoreQueryFilters().CountAsync());
        }
    }

    [Fact]
    public async Task Unrelated_events_and_foreign_receipts_are_ignored()
    {
        var (hook, db) = Build();
        using (db)
        {
            Assert.False((await hook.HandleAsync("""{"event":"payment.failed","payload":{}}""", default)).Handled);
            Assert.False((await hook.HandleAsync(OrderPaidBody("order_rcptid_11", 99900, "pay_x"), default)).Handled);  // a shopper order, not a subscription
            Assert.False((await hook.HandleAsync("not json", default)).Handled);
            Assert.Equal(0, await db.TenantBillingHistory.IgnoreQueryFilters().CountAsync());
        }
    }
}
