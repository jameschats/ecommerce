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
        var svc = new NotificationAdminService(db);
        var id = (await svc.ListTemplatesAsync())[0].Id;

        var updated = await svc.UpdateTemplateAsync(id, new UpdateNotificationTemplateRequest(
            "Your order shipped", "<p>Hi {{customerName}}</p><script>evil()</script>", true));

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
        var svc = new NotificationAdminService(db);
        var id = (await svc.ListTemplatesAsync())[0].Id;

        var updated = await svc.UpdateTemplateAsync(id, new UpdateNotificationTemplateRequest(null, "Order {{orderNumber}} placed", false));

        Assert.Equal("Order {{orderNumber}} placed", updated.Body);   // not HTML-mangled
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task Sender_round_trips_and_rejects_bad_reply_to()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new NotificationAdminService(db);

        await svc.UpdateSenderAsync(new NotificationSenderDto("My Store", "hello@store.com"));
        var s = await svc.GetSenderAsync();
        Assert.Equal("My Store", s.SenderName);
        Assert.Equal("hello@store.com", s.ReplyToEmail);

        await Assert.ThrowsAsync<AppException>(() => svc.UpdateSenderAsync(new NotificationSenderDto("X", "not-an-email")));
    }
}
