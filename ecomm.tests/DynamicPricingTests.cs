using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Plans;
using ecomm.api.Features.Pricing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// The pricing engine (v4 Phase 5) is deterministic C#, not an LLM — these tests verify the
/// arithmetic directly (no black-box result, per the plan's own verification checklist), plus the
/// merchant-control guardrails (bounds, lock, frequency cap) that must hold before the engine is
/// trusted to run at all.
/// </summary>
public class DynamicPricingTests
{
    private static (EcommerceDbContext db, PricingEngineService engine, PricingControlsService controls, PricingSuggestionService suggestions, AllowAllEntitlements entitlements) Setup()
    {
        var db = TestDb.New(tenantId: 1);
        var entitlements = new AllowAllEntitlements();
        var engine = new PricingEngineService(db, new PassThroughCredits(new StubAi()), new FixedTenant(1), entitlements);
        var controls = new PricingControlsService(db);
        var suggestions = new PricingSuggestionService(db);
        return (db, engine, controls, suggestions, entitlements);
    }

    private static Product NewProduct(long id, decimal price, decimal? min, decimal? max, bool locked = false, long categoryId = 1) => new()
    {
        ProductId = id, Name = $"Product {id}", Slug = $"p{id}", Sku = $"SKU{id}", Price = price,
        MinPrice = min, MaxPrice = max, PriceLocked = locked, Status = "Active", IsActive = true,
        CategoryId = categoryId, CreatedAt = DateTime.UtcNow,
    };

    private static void SeedInventory(EcommerceDbContext db, long productId, int available, int reorder) =>
        db.Inventory.Add(new Inventory { TenantId = 1, ProductId = productId, AvailableQty = available, ReorderLevel = reorder, CreatedAt = DateTime.UtcNow });

    [Fact]
    public async Task A_product_with_no_bounds_never_receives_a_suggestion()
    {
        var (db, engine, _, _, _) = Setup();
        using var _db = db;
        db.Products.Add(NewProduct(1, 100m, null, null));   // no MinPrice/MaxPrice set
        SeedInventory(db, 1, available: 1, reorder: 10);     // would otherwise trigger a strong low-stock signal
        await db.SaveChangesAsync();

        var created = await engine.GenerateSuggestionsForCurrentTenantAsync();

        Assert.Equal(0, created);
        Assert.Empty(db.PriceSuggestions);
    }

    [Fact]
    public async Task A_locked_product_never_receives_a_suggestion()
    {
        var (db, engine, _, _, _) = Setup();
        using var _db = db;
        db.Products.Add(NewProduct(1, 100m, 80m, 130m, locked: true));
        SeedInventory(db, 1, available: 1, reorder: 10);
        await db.SaveChangesAsync();

        var created = await engine.GenerateSuggestionsForCurrentTenantAsync();

        Assert.Equal(0, created);
    }

    [Theory]
    [InlineData(2, 10, 8)]      // ratio 0.2 (< 0.5) -> critically low -> +8%
    [InlineData(6, 10, 4)]      // ratio 0.6 (< 1.0) -> below reorder -> +4%
    [InlineData(35, 10, -5)]    // ratio 3.5 (> 3.0) -> overstocked -> -5%
    public async Task Inventory_signal_matches_the_documented_thresholds_exactly(int available, int reorder, int expectedPercent)
    {
        var (db, engine, _, _, _) = Setup();
        using var _db = db;
        db.Products.Add(NewProduct(1, 100m, 50m, 200m));
        SeedInventory(db, 1, available, reorder);
        await db.SaveChangesAsync();

        await engine.GenerateSuggestionsForCurrentTenantAsync();

        var s = db.PriceSuggestions.Single();
        Assert.Equal(expectedPercent, s.InventorySignalPercent);
        Assert.Equal(Math.Round(100m * (1 + expectedPercent / 100m), 2), s.SuggestedPrice);
    }

    [Fact]
    public async Task Normal_stock_with_no_active_season_rule_produces_no_suggestion()
    {
        var (db, engine, _, _, _) = Setup();
        using var _db = db;
        db.Products.Add(NewProduct(1, 100m, 50m, 200m));
        SeedInventory(db, 1, available: 15, reorder: 10);   // ratio 1.5 -> the "normal" band -> 0%
        await db.SaveChangesAsync();

        var created = await engine.GenerateSuggestionsForCurrentTenantAsync();

        Assert.Equal(0, created);   // zero total signal -> nothing to suggest, not a 0%-change row
    }

    [Fact]
    public async Task A_suggestion_is_clamped_to_the_products_own_bounds_never_beyond()
    {
        var (db, engine, _, _, _) = Setup();
        using var _db = db;
        db.Products.Add(NewProduct(1, 100m, 95m, 104m));    // ceiling well below what +8% would compute (108)
        SeedInventory(db, 1, available: 1, reorder: 10);     // triggers +8%
        await db.SaveChangesAsync();

        await engine.GenerateSuggestionsForCurrentTenantAsync();

        Assert.Equal(104m, db.PriceSuggestions.Single().SuggestedPrice);
    }

    [Fact]
    public async Task An_active_season_rule_applies_its_bias_and_an_expired_one_does_not()
    {
        var (db, engine, _, _, _) = Setup();
        using var _db = db;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Products.Add(NewProduct(1, 100m, 50m, 200m, categoryId: 1));
        db.PricingSeasonRules.Add(new PricingSeasonRule { Name = "Active Fest", StartDate = today.AddDays(-1), EndDate = today.AddDays(1), BiasPercent = 10m, CategoryId = 1, CreatedAt = DateTime.UtcNow });
        db.PricingSeasonRules.Add(new PricingSeasonRule { Name = "Long Expired", StartDate = today.AddDays(-30), EndDate = today.AddDays(-10), BiasPercent = 50m, CategoryId = 1, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await engine.GenerateSuggestionsForCurrentTenantAsync();

        var s = db.PriceSuggestions.Single();
        Assert.Equal(10m, s.SeasonalitySignalPercent);   // only the active rule counted, not the expired 50%
        Assert.Equal(110m, s.SuggestedPrice);
    }

    [Fact]
    public async Task Generating_twice_in_the_same_day_never_produces_a_second_suggestion_for_the_same_product()
    {
        var (db, engine, _, _, _) = Setup();
        using var _db = db;
        db.Products.Add(NewProduct(1, 100m, 50m, 200m));
        SeedInventory(db, 1, available: 1, reorder: 10);
        await db.SaveChangesAsync();

        var first = await engine.GenerateSuggestionsForCurrentTenantAsync();
        var second = await engine.GenerateSuggestionsForCurrentTenantAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Single(db.PriceSuggestions);
    }

    [Fact]
    public async Task Approving_a_suggestion_applies_the_price_and_stamps_the_approver()
    {
        var (db, engine, _, suggestions, _) = Setup();
        using var _db = db;
        db.Products.Add(NewProduct(1, 100m, 50m, 200m));
        SeedInventory(db, 1, available: 1, reorder: 10);
        await db.SaveChangesAsync();
        await engine.GenerateSuggestionsForCurrentTenantAsync();
        var id = db.PriceSuggestions.Single().PriceSuggestionId;

        var result = await suggestions.ApproveAsync(id, approvedByUserId: 7);

        Assert.Equal("Approved", result.Status);
        Assert.NotNull(result.AppliedAt);
        Assert.Equal(108m, db.Products.Single().Price);
        Assert.Equal(7, db.PriceSuggestions.Single().ApprovedByUserId);
    }

    [Fact]
    public async Task Rejecting_a_suggestion_leaves_the_price_unchanged()
    {
        var (db, engine, _, suggestions, _) = Setup();
        using var _db = db;
        db.Products.Add(NewProduct(1, 100m, 50m, 200m));
        SeedInventory(db, 1, available: 1, reorder: 10);
        await db.SaveChangesAsync();
        await engine.GenerateSuggestionsForCurrentTenantAsync();
        var id = db.PriceSuggestions.Single().PriceSuggestionId;

        var result = await suggestions.RejectAsync(id);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal(100m, db.Products.Single().Price);
    }

    [Fact]
    public async Task An_already_actioned_suggestion_cannot_be_approved_again()
    {
        var (db, engine, _, suggestions, _) = Setup();
        using var _db = db;
        db.Products.Add(NewProduct(1, 100m, 50m, 200m));
        SeedInventory(db, 1, available: 1, reorder: 10);
        await db.SaveChangesAsync();
        await engine.GenerateSuggestionsForCurrentTenantAsync();
        var id = db.PriceSuggestions.Single().PriceSuggestionId;
        await suggestions.RejectAsync(id);

        await Assert.ThrowsAsync<AppException>(() => suggestions.ApproveAsync(id, 7));
    }

    [Fact]
    public async Task Approval_is_rejected_if_the_product_was_locked_after_the_suggestion_was_generated()
    {
        var (db, engine, _, suggestions, _) = Setup();
        using var _db = db;
        db.Products.Add(NewProduct(1, 100m, 50m, 200m));
        SeedInventory(db, 1, available: 1, reorder: 10);
        await db.SaveChangesAsync();
        await engine.GenerateSuggestionsForCurrentTenantAsync();
        var id = db.PriceSuggestions.Single().PriceSuggestionId;
        db.Products.Single().PriceLocked = true;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppException>(() => suggestions.ApproveAsync(id, 7));
    }

    [Fact]
    public async Task Bulk_bounds_writes_explicit_values_from_current_price_and_skips_locked_products()
    {
        var (db, _, controls, _, _) = Setup();
        using var _db = db;
        db.Products.Add(NewProduct(1, 100m, null, null));
        db.Products.Add(NewProduct(2, 200m, null, null, locked: true));
        await db.SaveChangesAsync();

        var updated = await controls.BulkSetBoundsAsync(new BulkBoundsRequest(CategoryId: null, FloorPercent: 10m, CeilingPercent: 20m));

        Assert.Equal(1, updated);   // the locked product is skipped
        var p1 = db.Products.Single(p => p.ProductId == 1);
        Assert.Equal(90m, p1.MinPrice);
        Assert.Equal(120m, p1.MaxPrice);
        Assert.Null(db.Products.Single(p => p.ProductId == 2).MinPrice);
    }

    [Fact]
    public async Task Setting_a_floor_above_the_ceiling_is_rejected()
    {
        var (db, _, controls, _, _) = Setup();
        using var _db = db;
        db.Products.Add(NewProduct(1, 100m, null, null));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppException>(() => controls.SetAsync(1, new SetPricingControlsRequest(MinPrice: 150m, MaxPrice: 100m, PriceLocked: false)));
    }

    [Fact]
    public async Task Scheduled_generation_skips_tenants_without_the_dynamic_pricing_entitlement()
    {
        var db = TestDb.New(tenantId: 1);
        using var _db = db;
        db.Tenants.Add(new Tenant { TenantId = 1, Name = "T1", Slug = "t1", CreatedAt = DateTime.UtcNow });
        db.Products.Add(NewProduct(1, 100m, 50m, 200m));
        SeedInventory(db, 1, available: 1, reorder: 10);
        await db.SaveChangesAsync();

        var denyAll = new DenyAllEntitlements();
        var engine = new PricingEngineService(db, new PassThroughCredits(new StubAi()), new FixedTenant(1), denyAll);

        await engine.RunScheduledGenerationAsync();

        Assert.Empty(db.PriceSuggestions);
    }

    private sealed class StubAi : IAiService
    {
        public bool Enabled => true;
        public long EstimateCostMicros(AiCompletion completion) => 0;
        public Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default) =>
            Task.FromResult(new AiCompletion("Stock is low, so we're suggesting a small increase.", 20, 10, "test-model"));
    }

    private sealed class PassThroughCredits(IAiService ai) : IAiCreditService
    {
        public async Task<T> MeterAsync<T>(string feature, Func<IAiService, Task<(T, AiCompletion)>> action, CancellationToken ct = default)
        {
            var (result, _) = await action(ai);
            return result;
        }
        public Task<T> MeterImageAsync<T>(string feature, Func<IImageAiService, Task<(T, ImageResult)>> action, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AiBalanceDto> GetBalanceAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiUsageDto>> GetUsageAsync(int take = 50, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AiCreditPack?> GetPackAsync(int packId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> TopUpAsync(int packId, string reference, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class AllowAllEntitlements : IEntitlementService
    {
        public Task<bool> HasFeatureAsync(string featureKey, CancellationToken ct = default) => Task.FromResult(true);
        public Task EnsureCanAddProductsAsync(int adding = 1, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int?> RemainingProductSlotsAsync(CancellationToken ct = default) => Task.FromResult((int?)null);
        public Task EnsureCanUploadAsync(long addingBytes, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanPlaceOrderAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<PlanUsageDto> GetUsageAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class DenyAllEntitlements : IEntitlementService
    {
        public Task<bool> HasFeatureAsync(string featureKey, CancellationToken ct = default) => Task.FromResult(false);
        public Task EnsureCanAddProductsAsync(int adding = 1, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int?> RemainingProductSlotsAsync(CancellationToken ct = default) => Task.FromResult((int?)null);
        public Task EnsureCanUploadAsync(long addingBytes, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanPlaceOrderAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<PlanUsageDto> GetUsageAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
