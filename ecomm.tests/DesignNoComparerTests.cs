using ecomm.api.Features.Catalog.Services;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// The price list is sorted by design number, and the real catalogue's design numbers are
/// alphanumeric — so the cases that matter are the ones a plain string sort gets wrong.
/// </summary>
public sealed class DesignNoComparerTests
{
    private static List<string?> Sorted(params string?[] input)
        => input.OrderBy(x => x, DesignNoComparer.Instance).ToList();

    [Fact]
    public void NumbersSortNumerically_NotLexicographically()
    {
        Assert.Equal(["2", "9", "10", "100"], Sorted("100", "10", "9", "2"));
    }

    [Fact]
    public void SuffixedNumbersSortByTheirNumber()
    {
        // The real shape: a number with a range suffix. Lexicographically 1005FC would win.
        Assert.Equal(["225PAM", "1005FC", "1005SGL"], Sorted("1005SGL", "1005FC", "225PAM"));
    }

    [Fact]
    public void LeadingZerosAreIgnored()
    {
        Assert.Equal(["007", "8"], Sorted("8", "007"));
    }

    [Fact]
    public void ComparisonIsCaseInsensitive()
    {
        Assert.Equal(["10 E Wt", "10 Wt Col"], Sorted("10 Wt Col", "10 E Wt"));
        Assert.Equal(0, DesignNoComparer.Instance.Compare("1005fc", "1005FC"));
    }

    [Fact]
    public void ShorterSortsFirstWhenOneIsAPrefixOfTheOther()
    {
        Assert.Equal(["1005", "1005SGL", "1005SGLF"], Sorted("1005SGLF", "1005", "1005SGL"));
    }

    [Fact]
    public void NullsAndBlanksDoNotThrow()
    {
        Assert.Equal([null, "", "1"], Sorted(null, "1", ""));
    }
}
