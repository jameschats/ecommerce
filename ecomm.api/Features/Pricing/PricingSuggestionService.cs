using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Pricing;

public sealed record PriceSuggestionDto(
    long Id, long ProductId, string ProductName, decimal OldPrice, decimal SuggestedPrice,
    decimal InventorySignalPercent, decimal DemandSignalPercent, decimal SeasonalitySignalPercent,
    string? Reason, string Status, DateTime SuggestedAt, DateTime? ApprovedAt, DateTime? AppliedAt);

public interface IPricingSuggestionService
{
    /// <summary>The approval queue and the full audit log are the same read, filtered by status —
    /// every suggestion persists regardless of outcome, so "history" is just "status != null."</summary>
    Task<PagedResult<PriceSuggestionDto>> ListAsync(string? status, long? productId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Approving IS applying — same single-step "draft then approve" pattern the rest of
    /// v4 already uses, not a separate approve-then-apply flow the merchant would have to learn.</summary>
    Task<PriceSuggestionDto> ApproveAsync(long id, long approvedByUserId, CancellationToken ct = default);
    Task<PriceSuggestionDto> RejectAsync(long id, CancellationToken ct = default);
}

public sealed class PricingSuggestionService(EcommerceDbContext db) : IPricingSuggestionService
{
    public async Task<PagedResult<PriceSuggestionDto>> ListAsync(string? status, long? productId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = db.PriceSuggestions.AsNoTracking().Include(s => s.Product).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(s => s.Status == status);
        if (productId is { } pid) q = q.Where(s => s.ProductId == pid);

        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(s => s.PriceSuggestionId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new PriceSuggestionDto(
                s.PriceSuggestionId, s.ProductId, s.Product!.Name, s.OldPrice, s.SuggestedPrice,
                s.InventorySignalPercent, s.DemandSignalPercent, s.SeasonalitySignalPercent,
                s.Reason, s.Status, s.SuggestedAt, s.ApprovedAt, s.AppliedAt))
            .ToListAsync(ct);

        return new PagedResult<PriceSuggestionDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<PriceSuggestionDto> ApproveAsync(long id, long approvedByUserId, CancellationToken ct = default)
    {
        var s = await db.PriceSuggestions.Include(x => x.Product).FirstOrDefaultAsync(x => x.PriceSuggestionId == id, ct)
                 ?? throw new AppException("Suggestion not found.", StatusCodes.Status404NotFound);
        if (s.Status != "Pending") throw new AppException("This suggestion has already been actioned.", StatusCodes.Status400BadRequest);

        var product = s.Product ?? throw new AppException("Product not found.", StatusCodes.Status404NotFound);
        // Re-check bounds at approval time, not just at generation — a merchant may have tightened
        // the product's floor/ceiling (or locked it) between the suggestion being generated and reviewed.
        if (product.PriceLocked)
            throw new AppException("This product was locked after the suggestion was generated — approve after unlocking it, if that's intended.", StatusCodes.Status400BadRequest);
        if (product.MinPrice is { } min && s.SuggestedPrice < min || product.MaxPrice is { } max && s.SuggestedPrice > max)
            throw new AppException("This suggestion is now outside the product's current bounds — the bounds changed since it was generated.", StatusCodes.Status400BadRequest);

        var now = DateTime.UtcNow;
        product.Price = s.SuggestedPrice;
        product.UpdatedAt = now;
        s.Status = "Approved";
        s.ApprovedAt = now;
        s.ApprovedByUserId = approvedByUserId;
        s.AppliedAt = now;
        await db.SaveChangesAsync(ct);

        return new PriceSuggestionDto(s.PriceSuggestionId, s.ProductId, product.Name, s.OldPrice, s.SuggestedPrice,
            s.InventorySignalPercent, s.DemandSignalPercent, s.SeasonalitySignalPercent, s.Reason, s.Status, s.SuggestedAt, s.ApprovedAt, s.AppliedAt);
    }

    public async Task<PriceSuggestionDto> RejectAsync(long id, CancellationToken ct = default)
    {
        var s = await db.PriceSuggestions.Include(x => x.Product).FirstOrDefaultAsync(x => x.PriceSuggestionId == id, ct)
                 ?? throw new AppException("Suggestion not found.", StatusCodes.Status404NotFound);
        if (s.Status != "Pending") throw new AppException("This suggestion has already been actioned.", StatusCodes.Status400BadRequest);

        s.Status = "Rejected";   // ApprovedAt/AppliedAt intentionally stay null — this was never approved or applied
        await db.SaveChangesAsync(ct);

        return new PriceSuggestionDto(s.PriceSuggestionId, s.ProductId, s.Product!.Name, s.OldPrice, s.SuggestedPrice,
            s.InventorySignalPercent, s.DemandSignalPercent, s.SeasonalitySignalPercent, s.Reason, s.Status, s.SuggestedAt, s.ApprovedAt, s.AppliedAt);
    }
}
