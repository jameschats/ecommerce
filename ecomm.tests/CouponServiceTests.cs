using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Coupons;
using Xunit;

namespace ecomm.tests;

public sealed class CouponServiceTests
{
    private static Coupon Coupon(string code, string type, decimal value, decimal? cap = null,
        decimal? min = null, int? limit = null, int used = 0, bool active = true, DateTime? endsAt = null) => new()
    {
        TenantId = 1, Code = code, DiscountType = type, DiscountValue = value,
        MaxDiscountAmount = cap, MinOrderAmount = min, UsageLimit = limit, UsedCount = used,
        IsActive = active, EndsAt = endsAt, CreatedAt = DateTime.UtcNow,
    };

    private static async Task<(CouponService svc, EcommerceDbContext db)> WithCoupon(Coupon c)
    {
        var db = TestDb.New();
        db.Coupons.Add(c);
        await db.SaveChangesAsync();
        return (new CouponService(db), db);
    }

    [Fact]
    public async Task Percentage_UnderCap_DiscountsSubtotal()
    {
        var (svc, _) = await WithCoupon(Coupon("SAVE10", "Percentage", 10m, cap: 100m, min: 500m));
        var r = await svc.EvaluateAsync("SAVE10", userId: 1, subtotal: 660m);
        Assert.True(r.Ok);
        Assert.Equal(66m, r.Discount);
    }

    [Fact]
    public async Task Percentage_OverCap_IsCapped()
    {
        var (svc, _) = await WithCoupon(Coupon("HALF", "Percentage", 50m, cap: 100m));
        var r = await svc.EvaluateAsync("HALF", 1, 660m);   // 50% = 330 → capped to 100
        Assert.True(r.Ok);
        Assert.Equal(100m, r.Discount);
    }

    [Fact]
    public async Task Flat_DiscountsFixedAmount()
    {
        var (svc, _) = await WithCoupon(Coupon("FLAT50", "Flat", 50m));
        var r = await svc.EvaluateAsync("FLAT50", 1, 660m);
        Assert.True(r.Ok);
        Assert.Equal(50m, r.Discount);
    }

    [Fact]
    public async Task BelowMinOrder_IsRejected()
    {
        var (svc, _) = await WithCoupon(Coupon("BIG", "Flat", 100m, min: 1000m));
        var r = await svc.EvaluateAsync("BIG", 1, 660m);
        Assert.False(r.Ok);
        Assert.Equal(0m, r.Discount);
    }

    [Fact]
    public async Task Expired_IsRejected()
    {
        var (svc, _) = await WithCoupon(Coupon("OLD", "Flat", 50m, endsAt: DateTime.UtcNow.AddDays(-1)));
        var r = await svc.EvaluateAsync("OLD", 1, 660m);
        Assert.False(r.Ok);
    }

    [Fact]
    public async Task Inactive_IsRejected()
    {
        var (svc, _) = await WithCoupon(Coupon("OFF", "Flat", 50m, active: false));
        var r = await svc.EvaluateAsync("OFF", 1, 660m);
        Assert.False(r.Ok);
    }

    [Fact]
    public async Task UsageLimitReached_IsRejected()
    {
        var (svc, _) = await WithCoupon(Coupon("ONCE", "Flat", 50m, limit: 1, used: 1));
        var r = await svc.EvaluateAsync("ONCE", 1, 660m);
        Assert.False(r.Ok);
    }

    [Fact]
    public async Task UnknownCode_IsRejected()
    {
        var (svc, _) = await WithCoupon(Coupon("REAL", "Flat", 50m));
        var r = await svc.EvaluateAsync("NOPE", 1, 660m);
        Assert.False(r.Ok);
    }

    [Fact]
    public async Task EmptyCode_NoErrorNoDiscount()
    {
        var (svc, _) = await WithCoupon(Coupon("REAL", "Flat", 50m));
        var r = await svc.EvaluateAsync("", 1, 660m);
        Assert.False(r.Ok);
        Assert.Null(r.Error);
        Assert.Equal(0m, r.Discount);
    }
}
