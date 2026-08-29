using ecomm.api.Data.Entities;
using ecomm.api.Features.Apps.FirstParty;
using ecomm.api.Features.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ecomm.tests;

/// <summary>Second first-party app "Sales Digest" — emails the order count, revenue and top products
/// for the window.</summary>
public class SalesDigestTests
{
    private sealed class CapturingEmail : IEmailSender
    {
        public int Calls; public string? To; public string? Body;
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default, string? fromName = null, string? replyTo = null)
        { Calls++; To = toEmail; Body = htmlBody; return Task.CompletedTask; }
    }

    [Fact]
    public async Task Digest_summarises_orders_revenue_and_top_products()
    {
        var tenant = new FixedTenant(1);
        var db = TestDb.ForDatabase(System.Guid.NewGuid().ToString(), tenant);

        var app = new App { Name = "Sales Digest", Slug = "sales-digest", ClientId = "wcapp_salesdigest", Status = "listed", IsFirstParty = true, RequestedScopes = "orders:read", CreatedAt = System.DateTime.UtcNow };
        db.Apps.Add(app); await db.SaveChangesAsync();
        var inst = new AppInstallation { AppId = app.AppId, TenantId = 1, Status = "installed", GrantedScopes = "orders:read", InstalledAt = System.DateTime.UtcNow };
        db.AppInstallations.Add(inst); await db.SaveChangesAsync();
        db.AppSettings.Add(new AppSetting { TenantId = 1, AppInstallationId = inst.AppInstallationId, Key = "frequency", Value = "daily" });
        db.AppSettings.Add(new AppSetting { TenantId = 1, AppInstallationId = inst.AppInstallationId, Key = "recipientEmail", Value = "merchant@test.example" });

        var now = System.DateTime.UtcNow;
        db.Orders.Add(new Order { OrderId = 1, TenantId = 1, UserId = 1, OrderNumber = "O1", Status = "Paid", TotalAmount = 100, PlacedAt = now, CreatedAt = now });
        db.Orders.Add(new Order { OrderId = 2, TenantId = 1, UserId = 1, OrderNumber = "O2", Status = "Delivered", TotalAmount = 250, PlacedAt = now, CreatedAt = now });
        // An old order outside the daily window must be excluded.
        db.Orders.Add(new Order { OrderId = 3, TenantId = 1, UserId = 1, OrderNumber = "O3", Status = "Paid", TotalAmount = 999, PlacedAt = now.AddDays(-5), CreatedAt = now.AddDays(-5) });
        db.OrderItems.Add(new OrderItem { OrderId = 1, ProductId = 1, ProductName = "Widget A", Quantity = 3, UnitPrice = 20 });
        db.OrderItems.Add(new OrderItem { OrderId = 2, ProductId = 1, ProductName = "Widget A", Quantity = 2, UnitPrice = 20 });
        db.OrderItems.Add(new OrderItem { OrderId = 2, ProductId = 2, ProductName = "Widget B", Quantity = 1, UnitPrice = 50 });
        await db.SaveChangesAsync();

        var email = new CapturingEmail();
        var svc = new SalesDigestService(db, email, tenant, NullLogger<SalesDigestService>.Instance);

        var count = await svc.RunForCurrentTenantAsync();

        Assert.Equal(2, count);                 // old order excluded
        Assert.Equal(1, email.Calls);
        Assert.Equal("merchant@test.example", email.To);
        Assert.Contains("2</strong> order", email.Body);
        Assert.Contains("350", email.Body);     // revenue 100 + 250
        Assert.Contains("Widget A", email.Body);
    }
}
