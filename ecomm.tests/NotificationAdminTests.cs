using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Notifications;
using Xunit;

namespace ecomm.tests;

public class NotificationAdminTests
{
    [Fact]
    public async Task Update_email_template_sanitizes_html_and_sets_label()
    {
        using var db = TestDb.New(tenantId: 1);
        db.NotificationTemplates.Add(new NotificationTemplate { Code = "OrderShipped", Channel = "Email", Subject = "old", Body = "old", IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var svc = new NotificationAdminService(db, new NotificationChannelSettings(db));
        var id = (await svc.ListTemplatesAsync())[0].Id;

        var updated = await svc.UpdateTemplateAsync(id, new UpdateNotificationTemplateRequest(
            "Your order shipped", "<p>Hi {{customerName}}</p><script>evil()</script>", null, true));

        Assert.Equal("Order shipped", updated.Label);          // friendly label from code
        Assert.Contains("<p>Hi {{customerName}}</p>", updated.Body);
        Assert.DoesNotContain("<script", updated.Body);        // sanitized
    }

    [Fact]
    public async Task Sms_template_body_is_kept_as_plain_text()
    {
        using var db = TestDb.New(tenantId: 1);
        db.NotificationTemplates.Add(new NotificationTemplate { Code = "OrderPlaced", Channel = "SMS", Body = "x", IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var svc = new NotificationAdminService(db, new NotificationChannelSettings(db));
        var id = (await svc.ListTemplatesAsync())[0].Id;

        var updated = await svc.UpdateTemplateAsync(id, new UpdateNotificationTemplateRequest(null, "Order {{orderNumber}} placed", null, false));

        Assert.Equal("Order {{orderNumber}} placed", updated.Body);   // not HTML-mangled
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task WhatsApp_template_round_trips_the_external_template_id()
    {
        using var db = TestDb.New(tenantId: 1);
        db.NotificationTemplates.Add(new NotificationTemplate { Code = "OrderShipped", Channel = "WhatsApp", Body = "x", IsActive = false, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var svc = new NotificationAdminService(db, new NotificationChannelSettings(db));
        var id = (await svc.ListTemplatesAsync())[0].Id;

        var updated = await svc.UpdateTemplateAsync(id, new UpdateNotificationTemplateRequest(
            null, "Your order {{orderNumber}} shipped", "meta-tmpl-abc123", true));

        Assert.Equal("meta-tmpl-abc123", updated.ExternalTemplateId);
        Assert.True(updated.IsActive);
    }

    [Fact]
    public async Task Sender_round_trips_and_rejects_bad_reply_to()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new NotificationAdminService(db, new NotificationChannelSettings(db));

        await svc.UpdateSenderAsync(new NotificationSenderDto("My Store", "hello@store.com"));
        var s = await svc.GetSenderAsync();
        Assert.Equal("My Store", s.SenderName);
        Assert.Equal("hello@store.com", s.ReplyToEmail);

        await Assert.ThrowsAsync<AppException>(() => svc.UpdateSenderAsync(new NotificationSenderDto("X", "not-an-email")));
    }

    [Fact]
    public async Task Channel_toggles_default_to_all_enabled_with_no_settings_rows()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new NotificationAdminService(db, new NotificationChannelSettings(db));

        var toggles = await svc.GetChannelTogglesAsync();

        Assert.True(toggles.EmailEnabled);
        Assert.True(toggles.SmsEnabled);
        Assert.True(toggles.WhatsAppEnabled);
    }

    [Fact]
    public async Task Channel_toggles_round_trip_through_update_and_get()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new NotificationAdminService(db, new NotificationChannelSettings(db));

        await svc.UpdateChannelTogglesAsync(new ChannelTogglesDto(EmailEnabled: true, SmsEnabled: false, WhatsAppEnabled: false));
        var toggles = await svc.GetChannelTogglesAsync();

        Assert.True(toggles.EmailEnabled);
        Assert.False(toggles.SmsEnabled);
        Assert.False(toggles.WhatsAppEnabled);
    }
}
