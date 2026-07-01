using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Coupons;

/// <summary>Outcome of evaluating a code against a cart. <see cref="Ok"/> false ⇒ <see cref="Error"/> explains why.</summary>
public sealed record CouponResult(bool Ok, string? Error, decimal Discount, long? CouponId, string? Code, string? Description);

public sealed record AdminCouponDto(
    long CouponId, string Code, string? Description, string DiscountType, decimal DiscountValue,
    decimal? MaxDiscountAmount, decimal? MinOrderAmount, int? UsageLimit, int? PerUserLimit, int UsedCount,
    DateTime? StartsAt, DateTime? EndsAt, bool IsActive);

public sealed record SaveCouponRequest(
    string Code, string? Description, string DiscountType, decimal DiscountValue,
    decimal? MaxDiscountAmount, decimal? MinOrderAmount, int? UsageLimit, int? PerUserLimit,
    DateTime? StartsAt, DateTime? EndsAt, bool IsActive);

public interface ICouponService
{
    Task<CouponResult> EvaluateAsync(string? code, long userId, decimal subtotal, CancellationToken ct = default);
    Task RecordUsageAsync(long couponId, long userId, long orderId, decimal discount, CancellationToken ct = default);
    Task<List<AdminCouponDto>> ListAsync(CancellationToken ct = default);
    Task<AdminCouponDto> CreateAsync(SaveCouponRequest req, CancellationToken ct = default);
    Task<AdminCouponDto> UpdateAsync(long id, SaveCouponRequest req, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class CouponService : ICouponService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;

    public CouponService(EcommerceDbContext db) => _db = db;

    public async Task<CouponResult> EvaluateAsync(string? code, long userId, decimal subtotal, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return new CouponResult(false, null, 0m, null, null, null);
        var normalized = code.Trim();

        var coupon = await _db.Coupons.FirstOrDefaultAsync(
            c => c.TenantId == Tenant && c.Code == normalized, ct);
        if (coupon is null) return Fail("That coupon code isn't valid.");
        if (!coupon.IsActive) return Fail("This coupon is no longer active.");

        var now = DateTime.UtcNow;
        if (coupon.StartsAt is { } s && now < s) return Fail("This coupon isn't active yet.");
        if (coupon.EndsAt is { } e && now > e) return Fail("This coupon has expired.");
        if (coupon.MinOrderAmount is { } min && subtotal < min)
            return Fail($"Add items worth ₹{min:0.00} to use this coupon.");
        if (coupon.UsageLimit is { } limit && coupon.UsedCount >= limit)
            return Fail("This coupon has reached its usage limit.");
        if (coupon.PerUserLimit is { } perUser)
        {
            var used = await _db.CouponUsages.CountAsync(u => u.CouponId == coupon.CouponId && u.UserId == userId, ct);
            if (used >= perUser) return Fail("You've already used this coupon.");
        }

        var discount = coupon.DiscountType == "Percentage"
            ? Math.Round(subtotal * coupon.DiscountValue / 100m, 2)
            : coupon.DiscountValue;
        if (coupon.MaxDiscountAmount is { } cap && discount > cap) discount = cap;
        if (discount > subtotal) discount = subtotal; // never below zero
        if (discount <= 0m) return Fail("This coupon doesn't apply to your cart.");

        return new CouponResult(true, null, discount, coupon.CouponId, coupon.Code, coupon.Description);

        static CouponResult Fail(string msg) => new(false, msg, 0m, null, null, null);
    }

    public async Task RecordUsageAsync(long couponId, long userId, long orderId, decimal discount, CancellationToken ct = default)
    {
        _db.CouponUsages.Add(new CouponUsage
        {
            CouponId = couponId, UserId = userId, OrderId = orderId, DiscountAmount = discount, CreatedAt = DateTime.UtcNow,
        });
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.CouponId == couponId, ct);
        if (coupon is not null) coupon.UsedCount += 1;
        await _db.SaveChangesAsync(ct);
    }

    public Task<List<AdminCouponDto>> ListAsync(CancellationToken ct = default) =>
        _db.Coupons.AsNoTracking().Where(c => c.TenantId == Tenant).OrderByDescending(c => c.CouponId)
            .Select(c => new AdminCouponDto(c.CouponId, c.Code, c.Description, c.DiscountType, c.DiscountValue,
                c.MaxDiscountAmount, c.MinOrderAmount, c.UsageLimit, c.PerUserLimit, c.UsedCount,
                c.StartsAt, c.EndsAt, c.IsActive)).ToListAsync(ct);

    public async Task<AdminCouponDto> CreateAsync(SaveCouponRequest req, CancellationToken ct = default)
    {
        Validate(req);
        var code = req.Code.Trim().ToUpperInvariant();
        if (await _db.Coupons.AnyAsync(c => c.TenantId == Tenant && c.Code == code, ct))
            throw new AppException($"A coupon with code '{code}' already exists.", 409);

        var c = new Coupon { TenantId = Tenant, Code = code, CreatedAt = DateTime.UtcNow };
        Apply(c, req);
        _db.Coupons.Add(c);
        await _db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<AdminCouponDto> UpdateAsync(long id, SaveCouponRequest req, CancellationToken ct = default)
    {
        Validate(req);
        var c = await _db.Coupons.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.CouponId == id, ct)
            ?? throw new AppException("Coupon not found.", 404);
        var code = req.Code.Trim().ToUpperInvariant();
        if (code != c.Code && await _db.Coupons.AnyAsync(x => x.TenantId == Tenant && x.Code == code, ct))
            throw new AppException($"A coupon with code '{code}' already exists.", 409);
        c.Code = code;
        Apply(c, req);
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var c = await _db.Coupons.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.CouponId == id, ct)
            ?? throw new AppException("Coupon not found.", 404);
        _db.Coupons.Remove(c);
        await _db.SaveChangesAsync(ct);
    }

    private static void Validate(SaveCouponRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code)) throw new AppException("Coupon code is required.");
        if (req.DiscountType is not ("Flat" or "Percentage")) throw new AppException("Discount type must be Flat or Percentage.");
        if (req.DiscountValue <= 0) throw new AppException("Discount value must be greater than zero.");
        if (req.DiscountType == "Percentage" && req.DiscountValue > 100) throw new AppException("Percentage discount can't exceed 100.");
    }

    private static void Apply(Coupon c, SaveCouponRequest req)
    {
        c.Description = req.Description?.Trim();
        c.DiscountType = req.DiscountType;
        c.DiscountValue = req.DiscountValue;
        c.MaxDiscountAmount = req.MaxDiscountAmount;
        c.MinOrderAmount = req.MinOrderAmount;
        c.UsageLimit = req.UsageLimit;
        c.PerUserLimit = req.PerUserLimit;
        c.StartsAt = req.StartsAt;
        c.EndsAt = req.EndsAt;
        c.IsActive = req.IsActive;
    }

    private static AdminCouponDto ToDto(Coupon c) => new(c.CouponId, c.Code, c.Description, c.DiscountType, c.DiscountValue,
        c.MaxDiscountAmount, c.MinOrderAmount, c.UsageLimit, c.PerUserLimit, c.UsedCount, c.StartsAt, c.EndsAt, c.IsActive);
}
