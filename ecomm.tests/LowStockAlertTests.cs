using ecomm.api.Data.Entities;
using ecomm.api.Features.Apps.FirstParty;
using ecomm.api.Features.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ecomm.tests;

/// <summary>First-party "Low Stock Alerts" app — the sweep emails the merchant products at/below the
/// configured threshold, and only those.</summary>
public class LowStockAlertTests
{
    private sealed class CapturingEmail : IEmailSender
    {
        public int Calls; public string? To; public string? Subject; public string? Body;
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default, string? fromName = null, string? replyTo = null)
        { Calls++; To = toEmail; Subject = subject; Body = htmlBody; return Task.CompletedTask; }
    }

    [Fact]
    public async Task Emails_only_products_at_or_below_threshold()
    {
        var tenant = new FixedTenant(1);
        var db = TestDb.ForDatabase(System.Guid.NewGuid().ToString(), tenant);

        var app = new App { Name = "Low Stock Alerts", Slug = "low-stock-alerts", ClientId = "wcapp_lowstock", Status = "listed", IsFirstParty = true, RequestedScopes = "inventory:read", CreatedAt = System.DateTime.UtcNow };
        db.Apps.Add(app);
        await db.SaveChangesAsync();
        var inst = new AppInstallation { AppId = app.AppId, TenantId = 1, Status = "installed", GrantedScopes = "inventory:read", InstalledAt = System.DateTime.UtcNow };
        db.AppInstallations.Add(inst);
        await db.SaveChangesAsync();
        db.AppSettings.Add(new AppSetting { TenantId = 1, AppInstallationId = inst.AppInstallationId, Key = "threshold", Value = "5" });
        db.AppSettings.Add(new AppSetting { TenantId = 1, AppInstallationId = inst.AppInstallationId, Key = "recipientEmail", Value = "merchant@test.example" });

        db.Products.Add(new Product { ProductId = 1, TenantId = 1, CategoryId = 1, Sku = "LOW", Name = "Low Widget", Slug = "low-widget", Status = "Active", IsActive = true });
        db.Products.Add(new Product { ProductId = 2, TenantId = 1, CategoryId = 1, Sku = "OK", Name = "OK Widget", Slug = "ok-widget", Status = "Active", IsActive = true });
        db.Inventory.Add(new Inventory { ProductId = 1, TenantId = 1, AvailableQty = 2 });
        db.Inventory.Add(new Inventory { ProductId = 2, TenantId = 1, AvailableQty = 20 });
        await db.SaveChangesAsync();

        var email = new CapturingEmail();
        var svc = new LowStockAlertService(db, email, tenant, NullLogger<LowStockAlertService>.Instance);

        var count = await svc.RunForCurrentTenantAsync();

        Assert.Equal(1, count);
        Assert.Equal(1, email.Calls);
        Assert.Equal("merchant@test.example", email.To);
        Assert.Contains("Low Widget", email.Body);
        Assert.DoesNotContain("OK Widget", email.Body);
    }

    [Fact]
    public async Task No_email_when_nothing_is_low()
    {
        var tenant = new FixedTenant(1);
        var db = TestDb.ForDatabase(System.Guid.NewGuid().ToString(), tenant);
        var app = new App { Name = "Low Stock Alerts", Slug = "low-stock-alerts", ClientId = "wcapp_lowstock", Status = "listed", IsFirstParty = true, RequestedScopes = "inventory:read", CreatedAt = System.DateTime.UtcNow };
        db.Apps.Add(app); await db.SaveChangesAsync();
        var inst = new AppInstallation { AppId = app.AppId, TenantId = 1, Status = "installed", GrantedScopes = "inventory:read", InstalledAt = System.DateTime.UtcNow };
        db.AppInstallations.Add(inst); await db.SaveChangesAsync();
        db.AppSettings.Add(new AppSetting { TenantId = 1, AppInstallationId = inst.AppInstallationId, Key = "threshold", Value = "5" });
        db.Products.Add(new Product { ProductId = 1, TenantId = 1, CategoryId = 1, Sku = "OK", Name = "OK Widget", Slug = "ok-widget", Status = "Active", IsActive = true });
        db.Inventory.Add(new Inventory { ProductId = 1, TenantId = 1, AvailableQty = 50 });
        await db.SaveChangesAsync();

        var email = new CapturingEmail();
        var svc = new LowStockAlertService(db, email, tenant, NullLogger<LowStockAlertService>.Instance);
        var count = await svc.RunForCurrentTenantAsync();

        Assert.Equal(0, count);
        Assert.Equal(0, email.Calls);
    }
}
