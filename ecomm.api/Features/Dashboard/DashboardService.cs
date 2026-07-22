using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Features.Analytics;
using ecomm.api.Features.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Dashboard;

/// <summary>
/// One "get your store ready" setup step, computed from real store state.
/// <paramref name="CurrentValue"/> is an optional live detail shown beside the row — the connected
/// domain, the provider name, the product count — so a merchant can see the result, not just a tick.
/// </summary>
public sealed record ChecklistItem(
    string Key, string Label, string Description, bool Done, string ActionLabel, string ActionLink,
    string? CurrentValue = null);

public sealed record DashboardDto(
    AnalyticsSummaryDto Summary, IReadOnlyList<ChecklistItem> Checklist, int ChecklistDone, int ChecklistTotal);

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// The merchant admin Home: reuses the analytics summary for KPIs/needs-action and computes a
/// setup checklist from actual store state (products, storefront content, store details, tax, pages).
/// Tenant scoping comes from the global query filters — no manual TenantId filtering here.
/// </summary>
public sealed class DashboardService(
    EcommerceDbContext db,
    IAnalyticsService analytics,
    IStoreSettingsService settings,
    ICurrentTenantService currentTenant,
    IOptions<TenancyOptions> tenancy)
    : IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var summary = await analytics.SummaryAsync(ct);
        var s = await settings.GetAsync(ct);

        var productCount = await db.Products.CountAsync(ct);
        var hasStorefront = await db.PageSections.AnyAsync(ct);
        var hasStoreDetails = !string.IsNullOrWhiteSpace(s.StoreLegalName)
            && (!string.IsNullOrWhiteSpace(s.StoreEmail) || !string.IsNullOrWhiteSpace(s.StoreAddress));
        var hasTax = !string.IsNullOrWhiteSpace(s.StoreGstin)
            || string.Equals(s.TaxMode, "None", StringComparison.OrdinalIgnoreCase);

        // Online payments = a gateway the merchant actually switched live. COD is seeded on at signup,
        // so it can't stand in for this: without a gateway the store is COD-only.
        var payment = await db.TenantPaymentAccounts.FirstOrDefaultAsync(p => p.IsEnabled, ct);

        // Signup seeds one flat-rate "Standard Delivery" method so day-one checkout works. That seed must
        // not read as "done" — count it reviewed once a method has been edited or a pincode zone added.
        var shippingReviewed = await db.ShippingMethods.AnyAsync(m => m.UpdatedAt != null, ct)
            || await db.ShippingZones.AnyAsync(ct);

        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == currentTenant.CurrentTenantId, ct);
        var customDomainLive = tenant is { CustomDomainVerified: true } && !string.IsNullOrWhiteSpace(tenant.CustomDomain);

        var items = new List<ChecklistItem>
        {
            new("product", "Add your first product", "Stock your store with something to sell.",
                productCount > 0, "Add product", "/admin/products/new",
                productCount > 0 ? $"{productCount} product{(productCount == 1 ? "" : "s")}" : null),

            new("payments", "Accept online payments", "Connect a gateway so customers can pay by card or UPI.",
                payment is not null, "Payment settings", "/admin/payments",
                payment is not null ? $"{payment.Provider} connected" : "Cash on Delivery only"),

            new("shipping", "Set your shipping rates", "Review the default rate and add delivery zones.",
                shippingReviewed, "Shipping settings", "/admin/shipping",
                shippingReviewed ? null : "Using the default flat rate"),

            new("design", "Design your storefront", "Arrange your home page sections and theme.",
                hasStorefront, "Open builder", "/admin/pages"),

            new("details", "Add your store details", "Business name, contact email, address and GST.",
                hasStoreDetails && hasTax, "Store settings", "/admin/store-settings"),

            new("domain", "Add your own domain", "Connect a custom domain your customers will recognise.",
                customDomainLive, "Domain settings", "/admin/domain",
                customDomainLive ? tenant!.CustomDomain : StoreUrl(tenant?.Slug)),
        };

        return new DashboardDto(summary, items, items.Count(i => i.Done), items.Count);
    }

    /// <summary>The store's current public URL — shown on the domain row while it's still the default subdomain.</summary>
    private string? StoreUrl(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var baseDomain = tenancy.Value.BaseDomain;
        return string.IsNullOrEmpty(baseDomain) ? $"{slug}.localhost:4200" : $"{slug}.{baseDomain}";
    }
}
