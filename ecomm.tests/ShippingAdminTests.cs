using ecomm.api.Common.Exceptions;
using ecomm.api.Features.Shipping;
using Xunit;

namespace ecomm.tests;

public class ShippingAdminTests
{
    [Fact]
    public async Task Create_and_list_method_roundtrips()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new ShippingAdminService(db);

        await svc.CreateMethodAsync(new SaveShippingMethodRequest("Standard", null, 50m, 999m, 5, true));

        var methods = await svc.ListMethodsAsync();
        var m = Assert.Single(methods);
        Assert.Equal("Standard", m.Name);
        Assert.Equal(50m, m.BaseRate);
        Assert.Equal(999m, m.FreeShippingThreshold);
    }

    [Fact]
    public async Task Zone_pincode_range_is_validated()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new ShippingAdminService(db);

        // one-sided range rejected
        await Assert.ThrowsAsync<AppException>(() => svc.CreateZoneAsync(new SaveShippingZoneRequest("Z", "600001", null, 40m, true)));
        // start > end rejected
        await Assert.ThrowsAsync<AppException>(() => svc.CreateZoneAsync(new SaveShippingZoneRequest("Z", "600100", "600001", 40m, true)));

        var z = await svc.CreateZoneAsync(new SaveShippingZoneRequest("Metro", "600001", "699999", 40m, true));
        Assert.Equal("Metro", z.Name);
        Assert.True(z.IsServiceable);
    }
}
