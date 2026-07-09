using ecomm.api.Data.Context;
using ecomm.api.Features.Analytics;
using ecomm.api.Features.Settings;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Dashboard;

/// <summary>One "get your store ready" setup step, computed from real store state.</summary>
public sealed record ChecklistItem(string Key, string Label, string Description, bool Done, string ActionLabel, string ActionLink);

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
public sealed class DashboardService(EcommerceDbContext db, IAnalyticsService analytics, IStoreSettingsService settings)
    : IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var summary = await analytics.SummaryAsync(ct);
        var s = await settings.GetAsync(ct);

        var hasProducts = await db.Products.AnyAsync(ct);
        var hasStorefront = await db.PageSections.AnyAsync(ct);
        var hasCustomPage = await db.Pages.AnyAsync(p => p.Type == "Custom", ct);
        var hasStoreDetails = !string.IsNullOrWhiteSpace(s.StoreLegalName)
            && (!string.IsNullOrWhiteSpace(s.StoreEmail) || !string.IsNullOrWhiteSpace(s.StoreAddress));
        var hasTax = !string.IsNullOrWhiteSpace(s.StoreGstin)
            || string.Equals(s.TaxMode, "None", StringComparison.OrdinalIgnoreCase);

        var items = new List<ChecklistItem>
        {
            new("product", "Add your first product", "Stock your store with something to sell.", hasProducts, "Add product", "/admin/products/new"),
            new("design", "Design your storefront", "Arrange your home page sections and theme.", hasStorefront, "Open builder", "/admin/pages"),
            new("details", "Add your store details", "Business name, contact email and address.", hasStoreDetails, "Store settings", "/admin/store-settings"),
            new("tax", "Set up GST / tax", "Choose your tax mode and add your GSTIN.", hasTax, "Tax settings", "/admin/store-settings"),
            new("pages", "Add an About or Contact page", "Build trust with a few content pages.", hasCustomPage, "Add a page", "/admin/pages"),
        };

        return new DashboardDto(summary, items, items.Count(i => i.Done), items.Count);
    }
}
