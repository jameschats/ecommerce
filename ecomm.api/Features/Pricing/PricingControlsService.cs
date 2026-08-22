using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Pricing;

public sealed record ProductPricingControlsDto(long ProductId, string Name, decimal Price, decimal? MinPrice, decimal? MaxPrice, bool PriceLocked);
public sealed record SetPricingControlsRequest(decimal? MinPrice, decimal? MaxPrice, bool PriceLocked);
public sealed record BulkBoundsRequest(long? CategoryId, decimal FloorPercent, decimal CeilingPercent);
public sealed record PricingSeasonRuleDto(long Id, string Name, DateOnly StartDate, DateOnly EndDate, decimal BiasPercent, long? CategoryId);
public sealed record SavePricingSeasonRuleRequest(string Name, DateOnly StartDate, DateOnly EndDate, decimal BiasPercent, long? CategoryId);

public interface IPricingControlsService
{
    Task<IReadOnlyList<ProductPricingControlsDto>> ListControlsAsync(long? categoryId, CancellationToken ct = default);
    Task<ProductPricingControlsDto> SetAsync(long productId, SetPricingControlsRequest req, CancellationToken ct = default);

    /// <summary>Writes explicit MinPrice/MaxPrice onto every product in the category (or the whole
    /// catalog) from CURRENT price at call time — a one-time action, never a live recalculated
    /// percentage, so a merchant can edit one product's bounds afterward without a bulk rule
    /// silently overriding it again later.</summary>
    Task<int> BulkSetBoundsAsync(BulkBoundsRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<PricingSeasonRuleDto>> ListSeasonRulesAsync(CancellationToken ct = default);
    Task<PricingSeasonRuleDto> SaveSeasonRuleAsync(SavePricingSeasonRuleRequest req, CancellationToken ct = default);
    Task DeleteSeasonRuleAsync(long id, CancellationToken ct = default);
}

public sealed class PricingControlsService(EcommerceDbContext db) : IPricingControlsService
{
    private long Tenant => db.CurrentTenantId;

    public async Task<IReadOnlyList<ProductPricingControlsDto>> ListControlsAsync(long? categoryId, CancellationToken ct = default)
    {
        var q = db.Products.AsNoTracking().Where(p => !p.IsDeleted);
        if (categoryId is { } cid) q = q.Where(p => p.CategoryId == cid);
        return await q.OrderBy(p => p.Name)
            .Select(p => new ProductPricingControlsDto(p.ProductId, p.Name, p.Price, p.MinPrice, p.MaxPrice, p.PriceLocked))
            .ToListAsync(ct);
    }

    public async Task<ProductPricingControlsDto> SetAsync(long productId, SetPricingControlsRequest req, CancellationToken ct = default)
    {
        var p = await db.Products.FirstOrDefaultAsync(x => x.ProductId == productId && !x.IsDeleted, ct)
                 ?? throw new AppException("Product not found.", StatusCodes.Status404NotFound);

        if (req.MinPrice is { } min && req.MaxPrice is { } max && min > max)
            throw new AppException("Floor can't be higher than ceiling.", StatusCodes.Status400BadRequest);
        if (req.MinPrice is < 0 || req.MaxPrice is < 0)
            throw new AppException("Bounds can't be negative.", StatusCodes.Status400BadRequest);

        p.MinPrice = req.MinPrice;
        p.MaxPrice = req.MaxPrice;
        p.PriceLocked = req.PriceLocked;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new ProductPricingControlsDto(p.ProductId, p.Name, p.Price, p.MinPrice, p.MaxPrice, p.PriceLocked);
    }

    public async Task<int> BulkSetBoundsAsync(BulkBoundsRequest req, CancellationToken ct = default)
    {
        if (req.FloorPercent is < 0 or > 100) throw new AppException("Floor % must be between 0 and 100.", StatusCodes.Status400BadRequest);
        if (req.CeilingPercent < 0) throw new AppException("Ceiling % can't be negative.", StatusCodes.Status400BadRequest);

        var q = db.Products.Where(p => !p.IsDeleted && !p.PriceLocked);
        if (req.CategoryId is { } cid) q = q.Where(p => p.CategoryId == cid);
        var products = await q.ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var p in products)
        {
            p.MinPrice = Math.Round(p.Price * (1 - req.FloorPercent / 100m), 2);
            p.MaxPrice = Math.Round(p.Price * (1 + req.CeilingPercent / 100m), 2);
            p.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        return products.Count;
    }

    public async Task<IReadOnlyList<PricingSeasonRuleDto>> ListSeasonRulesAsync(CancellationToken ct = default) =>
        await db.PricingSeasonRules.AsNoTracking().OrderByDescending(r => r.StartDate)
            .Select(r => new PricingSeasonRuleDto(r.PricingSeasonRuleId, r.Name, r.StartDate, r.EndDate, r.BiasPercent, r.CategoryId))
            .ToListAsync(ct);

    public async Task<PricingSeasonRuleDto> SaveSeasonRuleAsync(SavePricingSeasonRuleRequest req, CancellationToken ct = default)
    {
        var name = (req.Name ?? "").Trim();
        if (name.Length == 0) throw new AppException("Name is required.", StatusCodes.Status400BadRequest);
        if (req.EndDate < req.StartDate) throw new AppException("End date must be on or after the start date.", StatusCodes.Status400BadRequest);

        var rule = new PricingSeasonRule
        {
            Name = name, StartDate = req.StartDate, EndDate = req.EndDate,
            BiasPercent = req.BiasPercent, CategoryId = req.CategoryId, CreatedAt = DateTime.UtcNow,
        };
        db.PricingSeasonRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return new PricingSeasonRuleDto(rule.PricingSeasonRuleId, rule.Name, rule.StartDate, rule.EndDate, rule.BiasPercent, rule.CategoryId);
    }

    public async Task DeleteSeasonRuleAsync(long id, CancellationToken ct = default)
    {
        var rule = await db.PricingSeasonRules.FirstOrDefaultAsync(r => r.PricingSeasonRuleId == id, ct)
                   ?? throw new AppException("Season rule not found.", StatusCodes.Status404NotFound);
        db.PricingSeasonRules.Remove(rule);
        await db.SaveChangesAsync(ct);
    }
}
