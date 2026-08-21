using ecomm.api.Data.Entities;
using ecomm.api.Features.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ecomm.tests;

public class NotificationRouterTests
{
    private sealed class FakeChannel : INotificationChannel
    {
        public string Key { get; }
        public bool Deliverable = true;
        public bool Succeeds = true;
        public int SendCount;

        public FakeChannel(string key) => Key = key;

        public bool CanDeliverTo(NotificationRecipient recipient) => Deliverable;

        public Task<bool> SendAsync(NotificationRecipient recipient, string subject, string body,
            IReadOnlyDictionary<string, string>? metadata, CancellationToken ct = default)
        {
            SendCount++;
            return Task.FromResult(Succeeds);
        }
    }

    private sealed class RecordingScheduler : IBackgroundJobScheduler
    {
        public int ScheduleCount;
        public string? LastCode;

        public void ScheduleNotificationRetry(string code, NotificationRecipient recipient, Dictionary<string, string> tokens, string category, TimeSpan delay)
        {
            ScheduleCount++;
            LastCode = code;
        }
    }

    private static NotificationRouter NewRouter(ecomm.api.Data.Context.EcommerceDbContext db,
        IEnumerable<INotificationChannel> channels, IBackgroundJobScheduler? scheduler = null)
        => new(db, channels, scheduler ?? new RecordingScheduler(), NullLogger<NotificationRouter>.Instance);

    private static void SeedTemplate(ecomm.api.Data.Context.EcommerceDbContext db, string code, string channel)
        => db.NotificationTemplates.Add(new NotificationTemplate
        {
            Code = code, Channel = channel, Subject = "Hi {{name}}", Body = "Body {{name}}", IsActive = true, CreatedAt = DateTime.UtcNow,
        });

    [Fact]
    public async Task Primary_channel_success_records_one_history_row_and_no_retry_scheduled()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedTemplate(db, "OrderConfirmation", "Email");
        SeedTemplate(db, "OrderConfirmation", "SMS");
        await db.SaveChangesAsync();

        var email = new FakeChannel("Email");
        var sms = new FakeChannel("SMS");
        var scheduler = new RecordingScheduler();
        var router = NewRouter(db, [email, sms], scheduler);

        var recipient = new NotificationRecipient(Email: "a@b.com", Phone: "9999999999");
        var sent = await router.DispatchAsync("OrderConfirmation", recipient, new Dictionary<string, string> { ["name"] = "Sam" });

        Assert.True(sent);
        Assert.Equal(1, email.SendCount);
        Assert.Equal(0, sms.SendCount);       // chain stops after the first success
        Assert.Equal(0, scheduler.ScheduleCount);

        var history = db.NotificationHistory.Single();
        Assert.Equal("Email", history.Channel);
        Assert.Equal("Sent", history.Status);
        Assert.Equal(1, history.AttemptNumber);
        Assert.NotNull(history.AttemptGroupId);
    }

    [Fact]
    public async Task Failed_primary_falls_back_to_next_channel_in_chain_and_correlates_history()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedTemplate(db, "OrderCancelled", "Email");
        SeedTemplate(db, "OrderCancelled", "SMS");
        await db.SaveChangesAsync();

        var email = new FakeChannel("Email") { Succeeds = false };
        var sms = new FakeChannel("SMS") { Succeeds = true };
        var router = NewRouter(db, [email, sms]);

        var recipient = new NotificationRecipient(Email: "a@b.com", Phone: "9999999999");
        var sent = await router.DispatchAsync("OrderCancelled", recipient, new Dictionary<string, string> { ["name"] = "Sam" });

        Assert.True(sent);
        Assert.Equal(1, email.SendCount);
        Assert.Equal(1, sms.SendCount);

        var rows = db.NotificationHistory.OrderBy(h => h.AttemptNumber).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("Failed", rows[0].Status);
        Assert.Equal("Email", rows[0].Channel);
        Assert.Equal(1, rows[0].AttemptNumber);
        Assert.Equal("Sent", rows[1].Status);
        Assert.Equal("SMS", rows[1].Channel);
        Assert.Equal(2, rows[1].AttemptNumber);
        Assert.Equal(rows[0].AttemptGroupId, rows[1].AttemptGroupId);   // same logical send
    }

    [Fact]
    public async Task Channel_the_recipient_cannot_be_reached_on_is_skipped_without_being_invoked()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedTemplate(db, "OrderCancelled", "Email");
        SeedTemplate(db, "OrderCancelled", "SMS");
        await db.SaveChangesAsync();

        var email = new FakeChannel("Email") { Deliverable = false };   // e.g. no email on file
        var sms = new FakeChannel("SMS") { Deliverable = true };
        var router = NewRouter(db, [email, sms]);

        var recipient = new NotificationRecipient(Phone: "9999999999");   // Email intentionally omitted
        var sent = await router.DispatchAsync("OrderCancelled", recipient, new Dictionary<string, string> { ["name"] = "Sam" });

        Assert.True(sent);
        Assert.Equal(0, email.SendCount);
        Assert.Equal(1, sms.SendCount);
        Assert.Single(db.NotificationHistory);   // no wasted attempt row for the skipped channel
    }

    [Fact]
    public async Task Channel_not_registered_in_DI_is_skipped_silently()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedTemplate(db, "OrderShipped", "SMS");
        await db.SaveChangesAsync();

        // OrderShipped's chain is [WhatsApp, SMS, Email] but only SMS is registered here —
        // this is exactly the pre-Track-C-launch state on the real chain.
        var sms = new FakeChannel("SMS");
        var router = NewRouter(db, [sms]);

        var recipient = new NotificationRecipient(Email: "a@b.com", Phone: "9999999999");
        var sent = await router.DispatchAsync("OrderShipped", recipient, new Dictionary<string, string> { ["name"] = "Sam" });

        Assert.True(sent);
        Assert.Equal(1, sms.SendCount);
    }

    [Fact]
    public async Task All_channels_failing_returns_false_and_schedules_exactly_one_retry()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedTemplate(db, "OrderCancelled", "Email");
        SeedTemplate(db, "OrderCancelled", "SMS");
        await db.SaveChangesAsync();

        var email = new FakeChannel("Email") { Succeeds = false };
        var sms = new FakeChannel("SMS") { Succeeds = false };
        var scheduler = new RecordingScheduler();
        var router = NewRouter(db, [email, sms], scheduler);

        var recipient = new NotificationRecipient(Email: "a@b.com", Phone: "9999999999");
        var sent = await router.DispatchAsync("OrderCancelled", recipient, new Dictionary<string, string> { ["name"] = "Sam" });

        Assert.False(sent);
        Assert.Equal(1, scheduler.ScheduleCount);
        Assert.Equal("OrderCancelled", scheduler.LastCode);
    }

    [Fact]
    public async Task RetryOnceAsync_does_not_itself_schedule_another_retry_even_on_failure()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedTemplate(db, "OrderCancelled", "Email");
        await db.SaveChangesAsync();

        var email = new FakeChannel("Email") { Succeeds = false };
        var scheduler = new RecordingScheduler();
        var router = NewRouter(db, [email], scheduler);

        await router.RetryOnceAsync("OrderCancelled", new NotificationRecipient(Email: "a@b.com"), new Dictionary<string, string> { ["name"] = "Sam" });

        Assert.Equal(0, scheduler.ScheduleCount);   // bounded to two total attempts, never an infinite retry loop
    }

    [Fact]
    public async Task Missing_template_for_a_channel_skips_to_the_next_channel_in_the_chain()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedTemplate(db, "OrderCancelled", "SMS");   // no Email template seeded
        await db.SaveChangesAsync();

        var email = new FakeChannel("Email");
        var sms = new FakeChannel("SMS");
        var router = NewRouter(db, [email, sms]);

        var recipient = new NotificationRecipient(Email: "a@b.com", Phone: "9999999999");
        var sent = await router.DispatchAsync("OrderCancelled", recipient, new Dictionary<string, string> { ["name"] = "Sam" });

        Assert.True(sent);
        Assert.Equal(0, email.SendCount);
        Assert.Equal(1, sms.SendCount);
    }

    [Fact]
    public async Task NotificationService_facade_sends_via_email_only_and_never_falls_back_to_sms()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedTemplate(db, "PasswordReset", "Email");
        await db.SaveChangesAsync();

        var email = new FakeChannel("Email");
        var sms = new FakeChannel("SMS");
        var router = NewRouter(db, [email, sms]);
        var svc = new NotificationService(router);

        var sent = await svc.SendEmailAsync("PasswordReset", "a@b.com", new Dictionary<string, string> { ["name"] = "Sam" });

        Assert.True(sent);
        Assert.Equal(1, email.SendCount);
        Assert.Equal(0, sms.SendCount);   // SMS channel has no phone on this recipient, correctly never tried
    }

    [Fact]
    public async Task Marketing_send_with_no_preference_row_is_blocked_by_default_not_sent()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedTemplate(db, "PromoBlast", "Email");
        await db.SaveChangesAsync();

        var email = new FakeChannel("Email");
        var scheduler = new RecordingScheduler();
        var router = NewRouter(db, [email], scheduler);

        var recipient = new NotificationRecipient(UserId: 42, Email: "a@b.com");
        var sent = await router.DispatchAsync("PromoBlast", recipient, new Dictionary<string, string> { ["name"] = "Sam" }, category: "marketing");

        Assert.False(sent);
        Assert.Equal(0, email.SendCount);           // never even attempted — no consent evidence
        Assert.Equal(0, scheduler.ScheduleCount);    // not a delivery failure, so no retry scheduled
        Assert.Empty(db.NotificationHistory);
    }

    [Fact]
    public async Task Marketing_send_reaches_an_opted_in_channel()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedTemplate(db, "PromoBlast", "Email");
        db.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            UserId = 42, Channel = "Email", Category = "marketing", IsOptedIn = true, OptedInAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var email = new FakeChannel("Email");
        var router = NewRouter(db, [email]);

        var recipient = new NotificationRecipient(UserId: 42, Email: "a@b.com");
        var sent = await router.DispatchAsync("PromoBlast", recipient, new Dictionary<string, string> { ["name"] = "Sam" }, category: "marketing");

        Assert.True(sent);
        Assert.Equal(1, email.SendCount);
    }

    [Fact]
    public async Task Transactional_send_is_never_gated_by_preferences_even_when_opted_out()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedTemplate(db, "OrderCancelled", "Email");
        db.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            UserId = 42, Channel = "Email", Category = "marketing", IsOptedIn = false, UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var email = new FakeChannel("Email");
        var router = NewRouter(db, [email]);

        var recipient = new NotificationRecipient(UserId: 42, Email: "a@b.com");
        var sent = await router.DispatchAsync("OrderCancelled", recipient, new Dictionary<string, string> { ["name"] = "Sam" });   // category defaults to transactional

        Assert.True(sent);
        Assert.Equal(1, email.SendCount);
    }

    [Fact]
    public async Task WhatsApp_registered_and_templated_wins_over_SMS_and_Email_in_the_order_chain()
    {
        using var db = TestDb.New(tenantId: 1);
        // OrderShipped's chain is [WhatsApp, SMS, Email] — WhatsApp should win once it's both
        // registered in DI and has an active template with an ExternalTemplateId configured.
        db.NotificationTemplates.Add(new NotificationTemplate
        {
            Code = "OrderShipped", Channel = "WhatsApp", Body = "Hi {{name}}, order {{orderNo}} shipped",
            ExternalTemplateId = "gupshup-tmpl-1", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        SeedTemplate(db, "OrderShipped", "SMS");
        await db.SaveChangesAsync();

        var whatsapp = new FakeChannel("WhatsApp");
        var sms = new FakeChannel("SMS");
        var router = NewRouter(db, [whatsapp, sms]);

        var recipient = new NotificationRecipient(Email: "a@b.com", Phone: "9999999999");
        var sent = await router.DispatchAsync("OrderShipped", recipient, new Dictionary<string, string> { ["name"] = "Sam", ["orderNo"] = "ORD-1" });

        Assert.True(sent);
        Assert.Equal(1, whatsapp.SendCount);
        Assert.Equal(0, sms.SendCount);
    }

    [Fact]
    public async Task WhatsApp_channel_metadata_carries_the_external_template_id_and_ordered_params()
    {
        using var db = TestDb.New(tenantId: 1);
        db.NotificationTemplates.Add(new NotificationTemplate
        {
            Code = "OrderShipped", Channel = "WhatsApp", Body = "Hi {{name}}, order {{orderNo}} shipped",
            ExternalTemplateId = "gupshup-tmpl-1", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        IReadOnlyDictionary<string, string>? capturedMetadata = null;
        var whatsapp = new CapturingChannel("WhatsApp", (r, s, b, m) => capturedMetadata = m);
        var router = NewRouter(db, [whatsapp]);

        var recipient = new NotificationRecipient(Phone: "9999999999");
        await router.DispatchAsync("OrderShipped", recipient, new Dictionary<string, string> { ["name"] = "Sam", ["orderNo"] = "ORD-1" });

        Assert.Equal("gupshup-tmpl-1", capturedMetadata!["ExternalTemplateId"]);
        var parts = capturedMetadata["TemplateParams"].Split(WhatsAppNotificationChannel.TemplateParamDelimiter);
        Assert.Equal(["Sam", "ORD-1"], parts);   // in the order {{name}} then {{orderNo}} appear in Body
    }

    [Fact]
    public async Task Real_WhatsApp_channel_declines_and_router_falls_back_when_ExternalTemplateId_is_missing()
    {
        using var db = TestDb.New(tenantId: 1);
        db.NotificationTemplates.Add(new NotificationTemplate
        {
            Code = "OrderShipped", Channel = "WhatsApp", Body = "Hi {{name}}",
            ExternalTemplateId = null, IsActive = true, CreatedAt = DateTime.UtcNow,   // not yet configured
        });
        SeedTemplate(db, "OrderShipped", "SMS");
        await db.SaveChangesAsync();

        var fakeProvider = new FakeWhatsAppProvider();
        var whatsapp = new WhatsAppNotificationChannel(fakeProvider, NullLogger<WhatsAppNotificationChannel>.Instance);
        var sms = new FakeChannel("SMS");
        var router = NewRouter(db, [whatsapp, sms]);

        var recipient = new NotificationRecipient(Phone: "9999999999");
        var sent = await router.DispatchAsync("OrderShipped", recipient, new Dictionary<string, string> { ["name"] = "Sam" });

        Assert.True(sent);
        Assert.Equal(0, fakeProvider.SendCount);   // real channel logic declined before ever calling the provider
        Assert.Equal(1, sms.SendCount);
    }

    [Fact]
    public async Task Real_WhatsApp_channel_sends_the_ordered_params_through_to_the_provider()
    {
        using var db = TestDb.New(tenantId: 1);
        db.NotificationTemplates.Add(new NotificationTemplate
        {
            Code = "OrderShipped", Channel = "WhatsApp", Body = "Hi {{name}}, order {{orderNo}} shipped",
            ExternalTemplateId = "gupshup-tmpl-1", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var fakeProvider = new FakeWhatsAppProvider();
        var whatsapp = new WhatsAppNotificationChannel(fakeProvider, NullLogger<WhatsAppNotificationChannel>.Instance);
        var router = NewRouter(db, [whatsapp]);

        var recipient = new NotificationRecipient(Phone: "9999999999");
        var sent = await router.DispatchAsync("OrderShipped", recipient, new Dictionary<string, string> { ["name"] = "Sam", ["orderNo"] = "ORD-1" });

        Assert.True(sent);
        Assert.Equal("gupshup-tmpl-1", fakeProvider.LastTemplateId);
        Assert.Equal(["Sam", "ORD-1"], fakeProvider.LastParameters);
    }

    private sealed class FakeWhatsAppProvider : ecomm.api.Features.WhatsApp.IWhatsAppProvider
    {
        public int SendCount;
        public string? LastTemplateId;
        public IReadOnlyList<string>? LastParameters;

        public Task<ecomm.api.Features.WhatsApp.WhatsAppSendResult> SendTemplateMessageAsync(string toPhone, string templateId,
            IReadOnlyList<string> parameters, CancellationToken ct = default)
        {
            SendCount++;
            LastTemplateId = templateId;
            LastParameters = parameters;
            return Task.FromResult(ecomm.api.Features.WhatsApp.WhatsAppSendResult.Ok("msg-1"));
        }

        public Task<ecomm.api.Features.WhatsApp.WhatsAppSendResult> SendSessionMessageAsync(string toPhone, string body, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class CapturingChannel : INotificationChannel
    {
        public string Key { get; }
        private readonly Action<NotificationRecipient, string, string, IReadOnlyDictionary<string, string>?> _onSend;
        public CapturingChannel(string key, Action<NotificationRecipient, string, string, IReadOnlyDictionary<string, string>?> onSend)
        {
            Key = key;
            _onSend = onSend;
        }
        public bool CanDeliverTo(NotificationRecipient recipient) => true;
        public Task<bool> SendAsync(NotificationRecipient recipient, string subject, string body,
            IReadOnlyDictionary<string, string>? metadata, CancellationToken ct = default)
        {
            _onSend(recipient, subject, body, metadata);
            return Task.FromResult(true);
        }
    }
}
