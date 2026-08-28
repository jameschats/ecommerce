using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Analytics;
using ecomm.api.Features.Dashboard;
using ecomm.api.Features.Settings;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

public class DashboardTests
{
    private static DashboardService NewService(out ecomm.api.Data.Context.EcommerceDbContext db, long tenantId = 1)
    {
        db = TestDb.New(tenantId);
        return new DashboardService(
            db, new AnalyticsService(db), new StoreSettingsService(db),
            new FixedTenant(tenantId),
            Options.Create(new TenancyOptions { BaseDomain = "wavcommerce.online" }));
    }

    [Fact]
    public async Task Fresh_store_has_full_checklist_none_done()
    {
        var svc = NewService(out var db);
        using var _ = db;

        var d = await svc.GetAsync();

        Assert.Equal(7, d.ChecklistTotal);
        Assert.Equal(0, d.ChecklistDone);
        Assert.All(d.Checklist, i => Assert.False(i.Done));
    }

    [Fact]
    public async Task Store_details_step_needs_both_details_and_a_tax_decision()
    {
        var svc = NewService(out var db);
        using var _ = db;

        // Tax decided ("None"), but no business name/contact yet — the combined step stays open.
        await new StoreSettingsService(db).UpdateAsync(
            new UpdateStoreSettingsRequest("None", null, null, null, false, null, null, null, null, false));
        Assert.False((await svc.GetAsync()).Checklist.Single(i => i.Key == "details").Done);

        await new StoreSettingsService(db).UpdateAsync(
            new UpdateStoreSettingsRequest("None", null, null, StoreLegalName: "Acme Sarees", false,
                StoreEmail: "hi@acme.test", null, null, null, false));

        var d = await svc.GetAsync();
        Assert.True(d.Checklist.Single(i => i.Key == "details").Done);
    }

    [Fact]
    public async Task Cod_only_store_does_not_complete_the_payments_step()
    {
        var svc = NewService(out var db);
        using var _ = db;

        // Signup seeds CodEnabled=true; that must not satisfy "accept online payments".
        await new StoreSettingsService(db).UpdateAsync(
            new UpdateStoreSettingsRequest("None", null, null, null, true, null, null, null, null, false));

        var payments = (await svc.GetAsync()).Checklist.Single(i => i.Key == "payments");

        Assert.False(payments.Done);
        Assert.Equal("Cash on Delivery only", payments.CurrentValue);
    }

    [Fact]
    public async Task Enabled_gateway_completes_the_payments_step_and_names_the_provider()
    {
        var svc = NewService(out var db);
        using var _ = db;

        db.TenantPaymentAccounts.Add(new TenantPaymentAccount { Provider = "Razorpay", IsEnabled = true });
        await db.SaveChangesAsync();

        var payments = (await svc.GetAsync()).Checklist.Single(i => i.Key == "payments");

        Assert.True(payments.Done);
        Assert.Equal("Razorpay connected", payments.CurrentValue);
    }

    [Fact]
    public async Task Seeded_default_shipping_method_does_not_count_as_reviewed()
    {
        var svc = NewService(out var db);
        using var _ = db;

        // Exactly what signup creates — never edited.
        db.ShippingMethods.Add(new ShippingMethod { Name = "Standard Delivery", BaseRate = 49m, UpdatedAt = null });
        await db.SaveChangesAsync();

        var shipping = (await svc.GetAsync()).Checklist.Single(i => i.Key == "shipping");

        Assert.False(shipping.Done);
        Assert.Equal("Using the default flat rate", shipping.CurrentValue);
    }

    [Fact]
    public async Task Editing_a_shipping_method_completes_the_shipping_step()
    {
        var svc = NewService(out var db);
        using var _ = db;

        db.ShippingMethods.Add(new ShippingMethod { Name = "Standard Delivery", BaseRate = 59m, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        Assert.True((await svc.GetAsync()).Checklist.Single(i => i.Key == "shipping").Done);
    }

    [Fact]
    public async Task Domain_step_shows_the_default_subdomain_until_a_custom_domain_is_verified()
    {
        var svc = NewService(out var db, tenantId: 7);
        using var _ = db;

        db.Tenants.Add(new Tenant { TenantId = 7, Name = "Acme", Slug = "acme" });
        await db.SaveChangesAsync();

        var domain = (await svc.GetAsync()).Checklist.Single(i => i.Key == "domain");

        Assert.False(domain.Done);
        Assert.Equal("acme.wavcommerce.online", domain.CurrentValue);
    }

    [Fact]
    public async Task Verified_custom_domain_completes_the_domain_step()
    {
        var svc = NewService(out var db, tenantId: 7);
        using var _ = db;

        db.Tenants.Add(new Tenant
        {
            TenantId = 7, Name = "Acme", Slug = "acme",
            CustomDomain = "shop.acme.in", CustomDomainVerified = true,
        });
        await db.SaveChangesAsync();

        var domain = (await svc.GetAsync()).Checklist.Single(i => i.Key == "domain");

        Assert.True(domain.Done);
        Assert.Equal("shop.acme.in", domain.CurrentValue);
    }

    [Fact]
    public async Task Only_the_auto_provisioned_starter_theme_does_not_complete_the_theme_step()
    {
        var svc = NewService(out var db);
        using var _ = db;

        // Exactly what signup provisions (see OnboardingService) — a single published starter theme,
        // never actually visited in the theme library.
        db.Themes.Add(new Theme { Name = "Minimal", Status = "Published", Source = "minimal" });
        await db.SaveChangesAsync();

        var theme = (await svc.GetAsync()).Checklist.Single(i => i.Key == "theme");

        Assert.False(theme.Done);
        Assert.Null(theme.CurrentValue);
    }

    [Fact]
    public async Task A_second_theme_completes_the_theme_step_and_names_the_published_one()
    {
        var svc = NewService(out var db);
        using var _ = db;

        db.Themes.Add(new Theme { Name = "Minimal", Status = "Draft", Source = "minimal" });
        db.Themes.Add(new Theme { Name = "Bloom", Status = "Published", Source = "bloom" });
        await db.SaveChangesAsync();

        var theme = (await svc.GetAsync()).Checklist.Single(i => i.Key == "theme");

        Assert.True(theme.Done);
        Assert.Equal("Bloom", theme.CurrentValue);
    }

    [Fact]
    public async Task Product_step_reports_the_count()
    {
        var svc = NewService(out var db);
        using var _ = db;

        db.Products.Add(new Product { Name = "Silk Saree", Slug = "silk-saree", Sku = "SS-1", Price = 999m });
        await db.SaveChangesAsync();

        var product = (await svc.GetAsync()).Checklist.Single(i => i.Key == "product");

        Assert.True(product.Done);
        Assert.Equal("1 product", product.CurrentValue);
    }
}
