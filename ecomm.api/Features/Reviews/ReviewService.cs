using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Reviews;

public sealed record ReviewDto(long ReviewId, string Author, int Rating, string? Title, string? Comment, bool IsVerifiedPurchase, DateTime CreatedAt);
public sealed record ReviewSummaryDto(double Average, int Count, int[] Distribution); // Distribution[0]=1-star … [4]=5-star
public sealed record ProductReviewsDto(ReviewSummaryDto Summary, PagedResult<ReviewDto> Reviews);
public sealed record SubmitReviewRequest(long ProductId, int Rating, string? Title, string? Comment);
public sealed record AdminReviewDto(long ReviewId, long ProductId, string ProductName, string Author, int Rating, string? Title, string? Comment, bool IsApproved, bool IsVerifiedPurchase, DateTime CreatedAt);
public sealed record ReviewEligibilityDto(bool CanReview, bool AlreadyReviewed);

public interface IReviewService
{
    Task<ProductReviewsDto> GetForProductAsync(long productId, int page, int pageSize, CancellationToken ct = default);
    Task<ReviewEligibilityDto> EligibilityAsync(long userId, long productId, CancellationToken ct = default);
    Task<ReviewDto> SubmitAsync(long userId, SubmitReviewRequest req, CancellationToken ct = default);
    Task<PagedResult<AdminReviewDto>> ListAdminAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task ApproveAsync(long reviewId, bool approved, CancellationToken ct = default);
    Task DeleteAsync(long reviewId, CancellationToken ct = default);
}

public sealed class ReviewService : IReviewService
{
    private const long Tenant = 1;
    private static readonly string[] PurchasedStatuses = { "Paid", "Packed", "Shipped", "Delivered" };
    private readonly EcommerceDbContext _db;
    private readonly INotificationFeedService _feed;

    public ReviewService(EcommerceDbContext db, INotificationFeedService feed)
    {
        _db = db;
        _feed = feed;
    }

    public async Task<ProductReviewsDto> GetForProductAsync(long productId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var approved = _db.Reviews.AsNoTracking().Where(r => r.TenantId == Tenant && r.ProductId == productId && r.IsApproved);

        var ratings = await approved.Select(r => (int)r.Rating).ToListAsync(ct);
        var dist = new int[5];
        foreach (var r in ratings) if (r is >= 1 and <= 5) dist[r - 1]++;
        var summary = new ReviewSummaryDto(
            ratings.Count == 0 ? 0 : Math.Round(ratings.Average(), 1), ratings.Count, dist);

        var total = ratings.Count;
        var items = await approved.OrderByDescending(r => r.ReviewId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ReviewDto(
                r.ReviewId,
                _db.Users.Where(u => u.UserId == r.UserId).Select(u => u.FullName).FirstOrDefault() ?? "Anonymous",
                r.Rating, r.Title, r.Comment, r.IsVerifiedPurchase, r.CreatedAt))
            .ToListAsync(ct);

        return new ProductReviewsDto(summary,
            new PagedResult<ReviewDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    public async Task<ReviewEligibilityDto> EligibilityAsync(long userId, long productId, CancellationToken ct = default)
    {
        var hasPurchased = await _db.OrderItems
            .Where(i => i.ProductId == productId)
            .Join(_db.Orders.Where(o => o.UserId == userId && PurchasedStatuses.Contains(o.Status)),
                  i => i.OrderId, o => o.OrderId, (i, o) => o.OrderId)
            .AnyAsync(ct);
        var alreadyReviewed = await _db.Reviews.AnyAsync(
            r => r.TenantId == Tenant && r.ProductId == productId && r.UserId == userId, ct);
        return new ReviewEligibilityDto(hasPurchased, alreadyReviewed);
    }

    public async Task<ReviewDto> SubmitAsync(long userId, SubmitReviewRequest req, CancellationToken ct = default)
    {
        if (req.Rating is < 1 or > 5) throw new AppException("Rating must be between 1 and 5.");
        var product = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == req.ProductId && p.TenantId == Tenant && !p.IsDeleted, ct)
            ?? throw new AppException("Product not found.", 404);

        // Only customers who bought this product may review it.
        var purchaseOrderId = await _db.OrderItems
            .Where(i => i.ProductId == req.ProductId)
            .Join(_db.Orders.Where(o => o.UserId == userId && PurchasedStatuses.Contains(o.Status)),
                  i => i.OrderId, o => o.OrderId, (i, o) => (long?)o.OrderId)
            .FirstOrDefaultAsync(ct);
        if (purchaseOrderId is null)
            throw new AppException("You can review a product only after purchasing it.", 403);

        var now = DateTime.UtcNow;
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.TenantId == Tenant && r.ProductId == req.ProductId && r.UserId == userId, ct);
        if (review is null)
        {
            review = new Review { TenantId = Tenant, ProductId = req.ProductId, UserId = userId, CreatedAt = now };
            _db.Reviews.Add(review);
        }
        review.Rating = (byte)req.Rating;
        review.Title = req.Title?.Trim();
        review.Comment = req.Comment?.Trim();
        review.OrderId = purchaseOrderId;
        review.IsVerifiedPurchase = purchaseOrderId is not null;
        review.IsApproved = false; // (re)moderate
        review.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        await _feed.NotifyAdminsAsync("PendingReview", "Review awaiting approval",
            $"{req.Rating}★ on {product.Name}", "/admin/reviews", ct);

        var author = await _db.Users.Where(u => u.UserId == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "Anonymous";
        return new ReviewDto(review.ReviewId, author, review.Rating, review.Title, review.Comment, review.IsVerifiedPurchase, review.CreatedAt);
    }

    public async Task<PagedResult<AdminReviewDto>> ListAdminAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Reviews.AsNoTracking().Where(r => r.TenantId == Tenant);
        if (status == "pending") q = q.Where(r => !r.IsApproved);
        else if (status == "approved") q = q.Where(r => r.IsApproved);

        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(r => r.ReviewId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new AdminReviewDto(
                r.ReviewId, r.ProductId,
                _db.Products.Where(p => p.ProductId == r.ProductId).Select(p => p.Name).FirstOrDefault() ?? "—",
                _db.Users.Where(u => u.UserId == r.UserId).Select(u => u.FullName).FirstOrDefault() ?? "Anonymous",
                r.Rating, r.Title, r.Comment, r.IsApproved, r.IsVerifiedPurchase, r.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<AdminReviewDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task ApproveAsync(long reviewId, bool approved, CancellationToken ct = default)
    {
        var review = await Find(reviewId, ct);
        review.IsApproved = approved;
        review.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long reviewId, CancellationToken ct = default)
    {
        var review = await Find(reviewId, ct);
        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Review> Find(long id, CancellationToken ct) =>
        await _db.Reviews.FirstOrDefaultAsync(r => r.TenantId == Tenant && r.ReviewId == id, ct)
        ?? throw new AppException("Review not found.", 404);
}
