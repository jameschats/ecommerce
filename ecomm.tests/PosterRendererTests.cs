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

    private static PosterSpec Spec(string headline, decimal? price = null, string primary = "#0d9488", string? font = "Poppins", string templateId = "bold-medallion", string format = "square") =>
        new("product", headline, price, "Shop Now", "Acme", true, true, "https://x/logo.png", null, primary, "#222222", "#f97316", font, TemplateId: templateId, Format: format);

    [Theory]
    [InlineData("bold-medallion")]
    [InlineData("minimal-type")]
    public async Task Renders_valid_svg_with_headline_and_cta_on_every_template(string templateId)
    {
        var svg = await new SvgPosterRenderer(new NoMedia()).RenderSvgAsync(Spec("Silk Saree", templateId: templateId));

        var doc = XDocument.Parse(svg);                                  // well-formed XML
        Assert.Equal("svg", doc.Root!.Name.LocalName);
        Assert.Contains("SILK SAREE", svg);                              // headline is rendered in caps
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
        Assert.Contains("TOM &amp; &quot;JERRY&quot;", svg);
        Assert.Contains("#111827", svg);                                 // fallback primary
    }

    [Fact]
    public async Task Unknown_template_id_falls_back_to_bold_medallion()
    {
        var svg = await new SvgPosterRenderer(new NoMedia()).RenderSvgAsync(Spec("Saree", templateId: "not-a-real-template"));
        Assert.Contains("SAREE", svg);   // still renders something sensible, doesn't throw
    }

    [Fact]
    public async Task Embeds_a_google_fonts_import_for_the_chosen_font()
    {
        var svg = await new SvgPosterRenderer(new NoMedia()).RenderSvgAsync(Spec("Saree", font: "Oswald"));
        Assert.Contains("@import url('https://fonts.googleapis.com/css2?family=Oswald", svg);
    }

    [Fact]
    public async Task Unknown_font_falls_back_to_a_known_display_face()
    {
        var svg = await new SvgPosterRenderer(new NoMedia()).RenderSvgAsync(Spec("Saree", font: "Comic Sans MS"));
        Assert.DoesNotContain("Comic Sans", svg);
        Assert.Contains("family=Poppins", svg);
    }

    [Fact]
    public void Exposes_the_template_library_and_font_list()
    {
        var renderer = new SvgPosterRenderer(new NoMedia());
        Assert.Contains(renderer.AvailableTemplates, t => t.Id == "bold-medallion" && t.UsesPhoto && t.Category == "Product Spotlight");
        Assert.Contains(renderer.AvailableTemplates, t => t.Id == "minimal-type" && !t.UsesPhoto && t.Category == "Sale & Offer");
        Assert.Contains("Poppins", renderer.AvailableFonts);
    }

    [Fact]
    public void Exposes_a_square_and_a_story_format_both_1080_wide()
    {
        var renderer = new SvgPosterRenderer(new NoMedia());
        Assert.Contains(renderer.AvailableFormats, f => f.Id == "square" && f.Width == 1080 && f.Height == 1080);
        Assert.Contains(renderer.AvailableFormats, f => f.Id == "story" && f.Width == 1080 && f.Height == 1920);
    }

    [Theory]
    [InlineData("bold-medallion", "square", 1080, 1080)]
    [InlineData("bold-medallion", "story", 1080, 1920)]
    [InlineData("minimal-type", "square", 1080, 1080)]
    [InlineData("minimal-type", "story", 1080, 1920)]
    public async Task Renders_the_requested_canvas_size_for_every_template(string templateId, string format, int expectedW, int expectedH)
    {
        var svg = await new SvgPosterRenderer(new NoMedia()).RenderSvgAsync(Spec("Saree", 2499m, templateId: templateId, format: format));

        var doc = XDocument.Parse(svg);
        Assert.Equal(expectedW.ToString(), doc.Root!.Attribute("width")!.Value);
        Assert.Equal(expectedH.ToString(), doc.Root.Attribute("height")!.Value);
        Assert.Contains("SAREE", svg);
        Assert.Contains("₹2499", svg);
    }

    [Fact]
    public async Task Unknown_format_falls_back_to_square()
    {
        var svg = await new SvgPosterRenderer(new NoMedia()).RenderSvgAsync(Spec("Saree", format: "not-a-real-format"));
        var doc = XDocument.Parse(svg);
        Assert.Equal("1080", doc.Root!.Attribute("width")!.Value);
        Assert.Equal("1080", doc.Root.Attribute("height")!.Value);
    }

    [Fact]
    public async Task Bold_medallion_at_the_square_baseline_is_unchanged_by_the_format_refactor()
    {
        // Locks in the original hand-tuned pixel value so the format refactor can't silently
        // regress the square poster that's already shipped and been visually reviewed. (NoMedia
        // never resolves a photo, so the medallion circle itself — also unchanged, cy=860/r=400
        // at H=1080 by construction — isn't asserted here; the CTA anchor is format-independent
        // math that every render exercises regardless of photo presence.)
        var svg = await new SvgPosterRenderer(new NoMedia()).RenderSvgAsync(Spec("Saree", 2499m));
        Assert.Contains("y=\"940\"", svg);      // CTA pill top, unchanged at H=1080
    }
}
