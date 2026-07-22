using System.Text.Json;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Plans;

/// <summary>What a tenant's plan allows, and how much of it they've used.</summary>
public sealed record PlanUsageDto(
    string? PlanName,
    int Products, int? MaxProducts,
    int OrdersThisPeriod, int? MaxOrders,
    DateTime? PeriodStart, DateTime? PeriodEnd,
    IReadOnlyList<string> Features);

public interface IEntitlementService
{
    /// <summary>True when the tenant's plan includes a named feature. Unknown keys are always false.</summary>
    Task<bool> HasFeatureAsync(string featureKey, CancellationToken ct = default);

    /// <summary>Throws 402 when adding another product would exceed the plan. No-op on unlimited plans.</summary>
    Task EnsureCanAddProductsAsync(int adding = 1, CancellationToken ct = default);

    /// <summary>
    /// How many more products the plan allows, or null for unlimited. For bulk paths that would
    /// otherwise re-count the catalogue once per row.
    /// </summary>
    Task<int?> RemainingProductSlotsAsync(CancellationToken ct = default);

    Task<PlanUsageDto> GetUsageAsync(CancellationToken ct = default);
}

/// <summary>
/// Enforces plan limits. Until now <c>Plan.MaxProducts</c>, <c>MaxOrders</c> and <c>Features</c> were
/// stored, shown on the pricing page and enforced nowhere — every store had unlimited everything.
///
/// Two deliberate asymmetries:
/// <list type="bullet">
/// <item>Products are enforced <b>on creation only</b>. An existing catalogue is never rejected, so a
/// plan change or a limit correction can't strand a merchant with data they can no longer edit.</item>
/// <item>Orders are <b>reported, not blocked</b>. Refusing a shopper's checkout because the merchant hit
/// a plan ceiling turns a billing conversation into lost revenue and an instant churn reason. The
/// number is surfaced to the merchant instead.</item>
/// </list>
/// </summary>
public sealed class EntitlementService(EcommerceDbContext db) : IEntitlementService
{
    public async Task<bool> HasFeatureAsync(string featureKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(featureKey)) return false;
        var plan = await CurrentPlanAsync(ct);
        return ParseFeatures(plan?.Features).Contains(featureKey, StringComparer.OrdinalIgnoreCase);
    }

    public async Task EnsureCanAddProductsAsync(int adding = 1, CancellationToken ct = default)
    {
        var plan = await CurrentPlanAsync(ct);
        if (plan?.MaxProducts is not { } max) return;   // no plan or unlimited

        var current = await db.Products.CountAsync(ct);
        if (current + adding <= max) return;

        var remaining = Math.Max(0, max - current);
        throw new AppException(
            remaining == 0
                ? $"Your {plan.Name} plan includes {max} products and you've used all of them. Upgrade to add more."
                : $"Your {plan.Name} plan includes {max} products — you can add {remaining} more. Upgrade to raise the limit.",
            StatusCodes.Status402PaymentRequired);
    }

    public async Task<int?> RemainingProductSlotsAsync(CancellationToken ct = default)
    {
        var plan = await CurrentPlanAsync(ct);
        if (plan?.MaxProducts is not { } max) return null;
        return Math.Max(0, max - await db.Products.CountAsync(ct));
    }

    public async Task<PlanUsageDto> GetUsageAsync(CancellationToken ct = default)
    {
        var sub = await db.TenantSubscriptions.AsNoTracking()
            .Include(s => s.Plan)
            .OrderByDescending(s => s.TenantSubscriptionId)
            .FirstOrDefaultAsync(ct);

        var (from, to) = PeriodOf(sub?.CurrentPeriodStart, sub?.CurrentPeriodEnd);
        var products = await db.Products.CountAsync(ct);
        var orders = await db.Orders.CountAsync(
            o => o.Status != "Draft" && !o.IsTest && o.PlacedAt >= from && o.PlacedAt <= to, ct);

        return new PlanUsageDto(
            sub?.Plan?.Name, products, sub?.Plan?.MaxProducts,
            orders, sub?.Plan?.MaxOrders, from, to,
            ParseFeatures(sub?.Plan?.Features));
    }

    private async Task<Data.Entities.Plan?> CurrentPlanAsync(CancellationToken ct) =>
        await db.TenantSubscriptions.AsNoTracking()
            .Include(s => s.Plan)
            .OrderByDescending(s => s.TenantSubscriptionId)
            .Select(s => s.Plan)
            .FirstOrDefaultAsync(ct);

    /// <summary>The billing period, falling back to the calendar month for stores without one.</summary>
    private static (DateTime from, DateTime to) PeriodOf(DateTime? start, DateTime? end)
    {
        if (start is { } s && end is { } e && e > s) return (s, e);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (monthStart, monthStart.AddMonths(1).AddTicks(-1));
    }

    /// <summary>
    /// <c>Plan.Features</c> is a free-form JSON string. Accepts either a list (<c>["growth"]</c>) or an
    /// object of flags (<c>{"growth": true}</c>), and treats anything unparseable as "no features" —
    /// a malformed plan row must not hand out entitlements.
    /// </summary>
    public static IReadOnlyList<string> ParseFeatures(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => doc.RootElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList(),
                JsonValueKind.Object => doc.RootElement.EnumerateObject()
                    .Where(p => p.Value.ValueKind == JsonValueKind.True)
                    .Select(p => p.Name)
                    .ToList(),
                _ => [],
            };
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
