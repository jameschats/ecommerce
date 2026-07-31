using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Collections;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Coupons;

/// <summary>Outcome of evaluating a code against a cart. <see cref="Ok"/> false ⇒ <see cref="Error"/> explains why.
/// <see cref="GiftProductId"/> is only set when the gift is currently real — resolved against the product's
/// active flag; stock is re-checked (and reserved) at order-placement time since it can change between quote
/// and checkout.</summary>
public sealed record CouponResult(bool Ok, string? Error, decimal Discount, bool FreeShipping, long? CouponId, string? Code, string? Description,
    long? GiftProductId = null, long? GiftVariantId = null, string? GiftProductName = null);

/// <summary>A cart line for targeted-discount evaluation.</summary>
public sealed record DiscountLine(long ProductId, decimal LineAmount);

public sealed record AdminCouponDto(
    long CouponId, string Code, string Method, string? Description, string DiscountType, decimal DiscountValue, bool FreeShipping,
    string AppliesTo, IReadOnlyList<long> TargetIds,
    decimal? MaxDiscountAmount, decimal? MinOrderAmount, int? UsageLimit, int? PerUserLimit, int UsedCount,
    DateTime? StartsAt, DateTime? EndsAt, bool IsActive, long? GiftProductId, string? GiftProductName);

public sealed record SaveCouponRequest(
    string Code, string Method, string? Description, string DiscountType, decimal DiscountValue, bool FreeShipping,
    string AppliesTo, IReadOnlyList<long>? TargetIds,
    decimal? MaxDiscountAmount, decimal? MinOrderAmount, int? UsageLimit, int? PerUserLimit,
    DateTime? StartsAt, DateTime? EndsAt, bool IsActive, long? GiftProductId = null);

public interface ICouponService
{
    Task<CouponResult> EvaluateAsync(string? code, long userId, decimal subtotal, CancellationToken ct = default);
    Task<CouponResult> EvaluateAsync(string? code, long userId, IReadOnlyList<DiscountLine> lines, CancellationToken ct = default);
    Task RecordUsageAsync(long couponId, long userId, long orderId, decimal discount, CancellationToken ct = default);
    Task<List<AdminCouponDto>> ListAsync(CancellationToken ct = default);
    Task<AdminCouponDto> CreateAsync(SaveCouponRequest req, CancellationToken ct = default);
    Task<AdminCouponDto> UpdateAsync(long id, SaveCouponRequest req, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class CouponService : ICouponService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;
    private readonly ICollectionService _collections;

    public CouponService(EcommerceDbContext db, ICollectionService collections)
    {
        _db = db;
        _collections = collections;
    }

    // Order-level entry point (no line detail) — treats the whole subtotal as eligible.
    public Task<CouponResult> EvaluateAsync(string? code, long userId, decimal subtotal, CancellationToken ct = default) =>
        EvaluateCoreAsync(code, userId, subtotal, lines: null, ct);

    // Line-aware entry point — enables product/collection targeting (eligible = matching lines).
    public Task<CouponResult> EvaluateAsync(string? code, long userId, IReadOnlyList<DiscountLine> lines, CancellationToken ct = default) =>
        EvaluateCoreAsync(code, userId, lines.Sum(l => l.LineAmount), lines, ct);

    private async Task<CouponResult> EvaluateCoreAsync(string? code, long userId, decimal subtotal, IReadOnlyList<DiscountLine>? lines, CancellationToken ct)
    {
        // A typed code takes precedence and reports why it fails; blank code → best applicable automatic discount.
        if (!string.IsNullOrWhiteSpace(code))
        {
            var normalized = code.Trim();
            var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.TenantId == Tenant && c.Code == normalized, ct);
            return coupon is null ? Fail("That coupon code isn't valid.") : await EvaluateCouponAsync(coupon, userId, subtotal, lines, ct);
        }

        var automatics = await _db.Coupons.Where(c => c.TenantId == Tenant && c.Method == "Automatic" && c.IsActive).ToListAsync(ct);
        CouponResult? best = null;
        foreach (var c in automatics)
        {
            var r = await EvaluateCouponAsync(c, userId, subtotal, lines, ct);
            if (!r.Ok) continue;
            // Prefer the bigger amount off; tie-break to the one that also gives free shipping.
            if (best is null || r.Discount > best.Discount || (r.Discount == best.Discount && r.FreeShipping && !best.FreeShipping))
                best = r;
        }
        return best ?? new CouponResult(false, null, 0m, false, null, null, null);
    }

    /// <summary>Validate a single coupon against the cart and compute its discount (off the eligible amount) + free-shipping.</summary>
    private async Task<CouponResult> EvaluateCouponAsync(Coupon coupon, long userId, decimal subtotal, IReadOnlyList<DiscountLine>? lines, CancellationToken ct)
    {
        if (!coupon.IsActive) return Fail("This coupon is no longer active.");
        var now = DateTime.UtcNow;
        if (coupon.StartsAt is { } s && now < s) return Fail("This coupon isn't active yet.");
        if (coupon.EndsAt is { } e && now > e) return Fail("This coupon has expired.");
        if (coupon.MinOrderAmount is { } min && subtotal < min)
            return Fail($"Add items worth ₹{min:0.00} to use this offer.");
        if (coupon.UsageLimit is { } limit && coupon.UsedCount >= limit)
            return Fail("This offer has reached its usage limit.");
        if (coupon.PerUserLimit is { } perUser)
        {
            var used = await _db.CouponUsages.CountAsync(u => u.CouponId == coupon.CouponId && u.UserId == userId, ct);
            if (used >= perUser) return Fail("You've already used this offer.");
        }

        // Targeted discounts apply to the matching lines only; order-level uses the whole subtotal.
        var eligible = await EligibleAmountAsync(coupon, subtotal, lines, ct);

        var discount = coupon.DiscountValue <= 0m ? 0m
            : coupon.DiscountType == "Percentage" ? Math.Round(eligible * coupon.DiscountValue / 100m, 2)
            : Math.Min(coupon.DiscountValue, eligible);
        if (coupon.MaxDiscountAmount is { } cap && discount > cap) discount = cap;
        if (discount > eligible) discount = eligible; // never exceed what it applies to

        // Applicable if it takes money off, grants free shipping, OR carries a (still-active) gift.
        string? giftName = null;
        long? giftProductId = null, giftVariantId = null;
        if (coupon.GiftProductId is { } gpid)
        {
            giftName = await _db.Products.Where(p => p.ProductId == gpid && p.TenantId == Tenant && p.IsActive && !p.IsDeleted)
                .Select(p => p.Name).FirstOrDefaultAsync(ct);
            if (giftName is not null) { giftProductId = gpid; giftVariantId = coupon.GiftVariantId; }
        }
        if (discount <= 0m && !coupon.FreeShipping && giftProductId is null) return Fail("This offer doesn't apply to your cart.");

        return new CouponResult(true, null, discount, coupon.FreeShipping, coupon.CouponId, coupon.Code, coupon.Description,
            giftProductId, giftVariantId, giftName);
    }

    private async Task<decimal> EligibleAmountAsync(Coupon coupon, decimal subtotal, IReadOnlyList<DiscountLine>? lines, CancellationToken ct)
    {
        if (coupon.AppliesTo == "Order" || lines is null) return subtotal;
        var targetProducts = await TargetProductIdsAsync(coupon, ct);
        return lines.Where(l => targetProducts.Contains(l.ProductId)).Sum(l => l.LineAmount);
    }

    private async Task<HashSet<long>> TargetProductIdsAsync(Coupon coupon, CancellationToken ct)
    {
        var targets = await _db.CouponTargets.Where(t => t.CouponId == coupon.CouponId).ToListAsync(ct);
        if (coupon.AppliesTo == "Products")
            return targets.Where(t => t.TargetType == "Product").Select(t => t.TargetId).ToHashSet();

        var set = new HashSet<long>();
        foreach (var t in targets.Where(t => t.TargetType == "Collection"))
            foreach (var m in await _collections.MembersAsync(t.TargetId, activeOnly: true, ct))
                set.Add(m.ProductId);
        return set;
    }

    private static CouponResult Fail(string msg) => new(false, msg, 0m, false, null, null, null);

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

    public async Task<List<AdminCouponDto>> ListAsync(CancellationToken ct = default)
    {
        var coupons = await _db.Coupons.AsNoTracking().Where(c => c.TenantId == Tenant).OrderByDescending(c => c.CouponId).ToListAsync(ct);
        var ids = coupons.Select(c => c.CouponId).ToList();
        var targets = await _db.CouponTargets.AsNoTracking().Where(t => ids.Contains(t.CouponId)).ToListAsync(ct);
        var giftIds = coupons.Where(c => c.GiftProductId is not null).Select(c => c.GiftProductId!.Value).Distinct().ToList();
        var giftNames = await _db.Products.AsNoTracking().Where(p => giftIds.Contains(p.ProductId)).ToDictionaryAsync(p => p.ProductId, p => p.Name, ct);
        return coupons.Select(c => ToDto(c, targets.Where(t => t.CouponId == c.CouponId).Select(t => t.TargetId).ToList(),
            c.GiftProductId is { } gid ? giftNames.GetValueOrDefault(gid) : null)).ToList();
    }

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
        await SaveTargetsAsync(c, req, ct);
        return ToDto(c, (req.TargetIds ?? []).ToList(), await GiftNameAsync(c.GiftProductId, ct));
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
        await SaveTargetsAsync(c, req, ct);
        await _db.SaveChangesAsync(ct);
        return ToDto(c, (req.TargetIds ?? []).ToList(), await GiftNameAsync(c.GiftProductId, ct));
    }

    private Task<string?> GiftNameAsync(long? productId, CancellationToken ct) =>
        productId is null ? Task.FromResult<string?>(null)
            : _db.Products.AsNoTracking().Where(p => p.ProductId == productId).Select(p => (string?)p.Name).FirstOrDefaultAsync(ct);

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var c = await _db.Coupons.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.CouponId == id, ct)
            ?? throw new AppException("Coupon not found.", 404);
        _db.CouponTargets.RemoveRange(await _db.CouponTargets.Where(t => t.CouponId == id).ToListAsync(ct));
        _db.Coupons.Remove(c);
        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveTargetsAsync(Coupon c, SaveCouponRequest req, CancellationToken ct)
    {
        _db.CouponTargets.RemoveRange(await _db.CouponTargets.Where(t => t.CouponId == c.CouponId).ToListAsync(ct));
        if (c.AppliesTo is "Products" or "Collections")
        {
            var type = c.AppliesTo == "Products" ? "Product" : "Collection";
            foreach (var tid in (req.TargetIds ?? []).Distinct())
                _db.CouponTargets.Add(new CouponTarget { CouponId = c.CouponId, TargetType = type, TargetId = tid });
        }
        await _db.SaveChangesAsync(ct);
    }

    private static void Validate(SaveCouponRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code)) throw new AppException("A code (or name for automatic offers) is required.");
        if (req.Method is not ("Code" or "Automatic")) throw new AppException("Method must be Code or Automatic.");
        if (req.DiscountType is not ("Flat" or "Percentage")) throw new AppException("Discount type must be Flat or Percentage.");
        // Free-shipping-only or gift-only offers can have a zero amount; plain amount offers need a positive value.
        if (!req.FreeShipping && req.GiftProductId is null && req.DiscountValue <= 0) throw new AppException("Discount value must be greater than zero.");
        if (req.DiscountValue < 0) throw new AppException("Discount value can't be negative.");
        if (req.DiscountType == "Percentage" && req.DiscountValue > 100) throw new AppException("Percentage discount can't exceed 100.");
    }

    private static readonly string[] AppliesToValues = { "Order", "Products", "Collections" };

    private static void Apply(Coupon c, SaveCouponRequest req)
    {
        c.Method = req.Method;
        c.Description = req.Description?.Trim();
        c.DiscountType = req.DiscountType;
        c.DiscountValue = req.DiscountValue;
        c.FreeShipping = req.FreeShipping;
        c.AppliesTo = AppliesToValues.FirstOrDefault(a => a.Equals(req.AppliesTo, StringComparison.OrdinalIgnoreCase)) ?? "Order";
        c.MaxDiscountAmount = req.MaxDiscountAmount;
        c.MinOrderAmount = req.MinOrderAmount;
        c.GiftProductId = req.GiftProductId;
        c.UsageLimit = req.UsageLimit;
        c.PerUserLimit = req.PerUserLimit;
        c.StartsAt = req.StartsAt;
        c.EndsAt = req.EndsAt;
        c.IsActive = req.IsActive;
    }

    private static AdminCouponDto ToDto(Coupon c, IReadOnlyList<long> targetIds, string? giftProductName) => new(c.CouponId, c.Code, c.Method, c.Description, c.DiscountType, c.DiscountValue,
        c.FreeShipping, c.AppliesTo, targetIds, c.MaxDiscountAmount, c.MinOrderAmount, c.UsageLimit, c.PerUserLimit, c.UsedCount, c.StartsAt, c.EndsAt, c.IsActive,
        c.GiftProductId, giftProductName);
}
