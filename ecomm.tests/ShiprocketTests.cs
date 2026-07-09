using ecomm.api.Features.Shipping.Shiprocket;
using Xunit;

namespace ecomm.tests;

public class ShiprocketTests
{
    [Fact]
    public void ParseCheapest_picks_lowest_serviceable_rate()
    {
        const string json = """
        {"status":200,"data":{"available_courier_companies":[
            {"courier_name":"Delhivery","rate":60,"estimated_delivery_days":"4"},
            {"courier_name":"Bluedart","rate":45.5,"estimated_delivery_days":"3"},
            {"courier_name":"Ekart","rate":52,"estimated_delivery_days":"5"}
        ]}}
        """;

        var r = ShiprocketClient.ParseCheapest(json);

        Assert.NotNull(r);
        Assert.Equal("Bluedart", r!.CourierName);
        Assert.Equal(45.5m, r.Rate);
        Assert.Equal(3, r.EstimatedDays);
    }

    [Fact]
    public void ParseCheapest_returns_null_when_no_couriers_or_bad_json()
    {
        Assert.Null(ShiprocketClient.ParseCheapest("{}"));
        Assert.Null(ShiprocketClient.ParseCheapest("not json"));
        Assert.Null(ShiprocketClient.ParseCheapest("""{"data":{"available_courier_companies":[]}}"""));
    }

    [Fact]
    public async Task Null_client_is_disabled_and_returns_no_rate()
    {
        var client = new NullShiprocketClient();
        Assert.False(client.Enabled);
        Assert.Null(await client.GetCheapestRateAsync("600001", 0.5m, false));
    }
}
