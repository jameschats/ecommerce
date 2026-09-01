using System.Xml.Linq;
using ecomm.api.Features.Media;
using ecomm.api.Features.MarketingStudio;
using Xunit;

namespace ecomm.tests;

public class PosterRendererTests
{
    /// <summary>Media stub — no image is "ours", so posters render typographically (no fetch).</summary>
    private sealed class NoMedia : IMediaStorage
    {
        public Task<StoredFile> SaveAsync(Stream data, string n, string c, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> SaveVariantAsync(string u, string s, Stream d, CancellationToken ct = default) => Task.FromResult(false);
        public Task<Stream?> OpenReadAsync(string url, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
    }

    private static PosterSpec Spec(string headline, decimal? price = null, string primary = "#0d9488", string? font = "Poppins") =>
        new("product", headline, price, "Shop Now", "Acme", true, true, "https://x/logo.png", null, primary, "#222222", "#f97316", font);

    [Fact]
    public async Task Renders_valid_svg_with_headline_and_cta()
    {
        var svg = await new SvgPosterRenderer(new NoMedia()).RenderSvgAsync(Spec("Silk Saree"));

        var doc = XDocument.Parse(svg);                                  // well-formed XML
        Assert.Equal("svg", doc.Root!.Name.LocalName);
        Assert.Contains("Silk Saree", svg);
        Assert.Contains("Shop Now", svg);
        Assert.Contains("1080", svg);
    }

    [Fact]
    public async Task Includes_a_price_badge_when_a_price_is_given()
    {
        var withPrice = await new SvgPosterRenderer(new NoMedia()).RenderSvgAsync(Spec("Saree", 2499m));
        var without = await new SvgPosterRenderer(new NoMedia()).RenderSvgAsync(Spec("Saree"));
        Assert.Contains("₹2499", withPrice);
        Assert.DoesNotContain("₹", without);
    }

    [Fact]
    public async Task Escapes_headline_and_falls_back_on_a_bad_colour()
    {
        // A quote/ampersand in the headline must not break the XML; a junk colour falls back safely.
        var svg = await new SvgPosterRenderer(new NoMedia()).RenderSvgAsync(Spec("Tom & \"Jerry\"", primary: "not-a-color"));
        var doc = XDocument.Parse(svg);                                  // still well-formed
        Assert.NotNull(doc.Root);
        Assert.Contains("Tom &amp; &quot;Jerry&quot;", svg);
        Assert.Contains("#111827", svg);                                 // fallback primary
    }
}
