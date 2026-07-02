using ecomm.api.Features.Checkout;
using Xunit;

namespace ecomm.tests;

/// <summary>The GST engine — the most correctness-critical piece of the money path.</summary>
public sealed class TaxServiceTests
{
    private static TaxService NewService() => new(TestDb.New());

    [Fact]
    public void Exclusive_IntraState_SplitsCgstSgst()
    {
        var r = NewService().ComputeLine(660m, 12m, interState: false, TaxMode.Exclusive);
        Assert.Equal(660m, r.Net);
        Assert.Equal(79.20m, r.Tax);
        Assert.Equal(39.60m, r.Cgst);
        Assert.Equal(39.60m, r.Sgst);
        Assert.Equal(0m, r.Igst);
    }

    [Fact]
    public void Exclusive_InterState_UsesIgst()
    {
        var r = NewService().ComputeLine(660m, 12m, interState: true, TaxMode.Exclusive);
        Assert.Equal(79.20m, r.Tax);
        Assert.Equal(79.20m, r.Igst);
        Assert.Equal(0m, r.Cgst);
        Assert.Equal(0m, r.Sgst);
    }

    [Fact]
    public void Inclusive_ReverseCalculatesTaxOutOfPrice()
    {
        var r = NewService().ComputeLine(660m, 12m, interState: false, TaxMode.Inclusive);
        Assert.Equal(589.29m, r.Net);
        Assert.Equal(70.71m, r.Tax);
        Assert.Equal(660m, r.Net + r.Tax);          // net + tax == listed price
        Assert.Equal(35.36m, r.Cgst);
        Assert.Equal(35.35m, r.Sgst);
    }

    [Fact]
    public void None_ChargesNoTax()
    {
        var r = NewService().ComputeLine(660m, 12m, interState: false, TaxMode.None);
        Assert.Equal(660m, r.Net);
        Assert.Equal(0m, r.Tax);
        Assert.Equal(0m, r.Rate);
    }

    [Fact]
    public void ZeroRate_ChargesNoTax()
    {
        var r = NewService().ComputeLine(660m, 0m, interState: false, TaxMode.Exclusive);
        Assert.Equal(660m, r.Net);
        Assert.Equal(0m, r.Tax);
    }
}
