using ecomm.api.Data.Entities;
using ecomm.api.Features.Coupons;
using Xunit;

namespace ecomm.tests;

public class CouponDiscountTests
{
    private static Coupon Coupon(string code, string method, string type, decimal value,
        bool freeShip = false, decimal? min = null, bool active = true) => new()
    {
        Code = code, Method = method, DiscountType = type, DiscountValue = value,
        FreeShipping = freeShip, MinOrderAmount = min, IsActive = active, CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task Typed_code_percentage_applies()
    {
        using var db = TestDb.New(tenantId: 1);
        db.Coupons.Add(Coupon("SAVE10", "Code", "Percentage", 10m));
        await db.SaveChangesAsync();
        var svc = new CouponService(db);

        var r = await svc.EvaluateAsync("SAVE10", userId: 1, subtotal: 1000m);

        Assert.True(r.Ok);
        Assert.Equal(100m, r.Discount);
        Assert.False(r.FreeShipping);
    }

    [Fact]
    public async Task Blank_code_applies_the_best_automatic_offer()
    {
        using var db = TestDb.New(tenantId: 1);
        db.Coupons.Add(Coupon("AUTO50", "Automatic", "Flat", 50m));
        db.Coupons.Add(Coupon("AUTO80", "Automatic", "Flat", 80m));
        db.Coupons.Add(Coupon("CODEONLY", "Code", "Flat", 200m));   // code discounts never auto-apply
        await db.SaveChangesAsync();
        var svc = new CouponService(db);

        var r = await svc.EvaluateAsync(code: null, userId: 1, subtotal: 1000m);

        Assert.True(r.Ok);
        Assert.Equal(80m, r.Discount);          // best automatic, not the code-only 200
    }

    [Fact]
    public async Task Free_shipping_automatic_applies_with_zero_amount()
    {
        using var db = TestDb.New(tenantId: 1);
        db.Coupons.Add(Coupon("FREESHIP", "Automatic", "Flat", 0m, freeShip: true));
        await db.SaveChangesAsync();
        var svc = new CouponService(db);

        var r = await svc.EvaluateAsync(code: null, userId: 1, subtotal: 1000m);

        Assert.True(r.Ok);
        Assert.Equal(0m, r.Discount);
        Assert.True(r.FreeShipping);
    }

    [Fact]
    public async Task Automatic_below_min_order_does_not_apply()
    {
        using var db = TestDb.New(tenantId: 1);
        db.Coupons.Add(Coupon("AUTO", "Automatic", "Flat", 100m, min: 2000m));
        await db.SaveChangesAsync();
        var svc = new CouponService(db);

        var r = await svc.EvaluateAsync(code: null, userId: 1, subtotal: 1000m);

        Assert.False(r.Ok);   // no applicable automatic
        Assert.Equal(0m, r.Discount);
    }

    [Fact]
    public async Task Invalid_code_reports_error()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new CouponService(db);

        var r = await svc.EvaluateAsync("NOPE", userId: 1, subtotal: 1000m);

        Assert.False(r.Ok);
        Assert.NotNull(r.Error);
    }
}
