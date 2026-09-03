using ecomm.api.Features.Media;
using ecomm.api.Features.MarketingStudio;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

public class PosterDocumentValidatorTests
{
    private sealed class FakeRenderer : IPosterRenderer
    {
        public Task<string> RenderSvgAsync(PosterSpec spec, CancellationToken ct = default) => throw new NotImplementedException();
        public IReadOnlyList<string> AvailableFonts { get; } = new[] { "Poppins" };
        public IReadOnlyList<PosterTemplateInfo> AvailableTemplates { get; } = new[] { new PosterTemplateInfo("bold-medallion", "Bold Medallion", "d", true, "Product Spotlight") };
        public IReadOnlyList<PosterFormatInfo> AvailableFormats { get; } = new[]
        {
            new PosterFormatInfo("square", "Square", 1080, 1080), new PosterFormatInfo("story", "Story", 1080, 1920),
        };
    }

    private static IPosterDocumentValidator New() =>
        new PosterDocumentValidator(new FakeRenderer(), Options.Create(new MediaOptions { RequestPath = "/uploads" }));

    private static PosterLayer TextLayer(string id = "headline", string? text = "Hello", double opacity = 1) =>
        new(id, "text", 72, 200, 500, 200, 0, opacity, 1, Role: "headline", Text: text, FontFamily: "Poppins", FontSize: 80, FontWeight: "900", Color: "#ffffff");

    private static PosterDocument ValidDoc(params PosterLayer[] layers) =>
        new("layers-v1", new PosterDocFormat(1080, 1080), new PosterDocBackground("color", "#111827"),
            layers.Length > 0 ? layers : new[] { TextLayer() });

    [Fact]
    public void A_well_formed_document_has_no_errors()
    {
        var errors = New().Validate(ValidDoc());
        Assert.Empty(errors);
    }

    [Fact]
    public void Rejects_an_unknown_spec_version()
    {
        var doc = ValidDoc() with { SpecVersion = "layers-v2" };
        Assert.Contains(New().Validate(doc), e => e.Contains("version"));
    }

    [Fact]
    public void Rejects_a_canvas_size_not_in_the_format_registry()
    {
        var doc = ValidDoc() with { Format = new PosterDocFormat(999, 999) };
        Assert.Contains(New().Validate(doc), e => e.Contains("format"));
    }

    [Fact]
    public void Accepts_the_story_format()
    {
        var doc = ValidDoc() with { Format = new PosterDocFormat(1080, 1920) };
        Assert.Empty(New().Validate(doc));
    }

    [Fact]
    public void Rejects_an_empty_layer_list()
    {
        var doc = ValidDoc() with { Layers = Array.Empty<PosterLayer>() };
        Assert.Contains(New().Validate(doc), e => e.Contains("at least one layer"));
    }

    [Fact]
    public void Rejects_more_than_forty_layers()
    {
        var layers = Enumerable.Range(0, 41).Select(i => TextLayer($"t{i}")).ToArray();
        Assert.Contains(New().Validate(ValidDoc(layers)), e => e.Contains("Too many layers"));
    }

    [Fact]
    public void Rejects_an_unknown_layer_type()
    {
        var doc = ValidDoc(TextLayer() with { Type = "video" });
        Assert.Contains(New().Validate(doc), e => e.Contains("unknown type"));
    }

    [Fact]
    public void Rejects_a_text_layer_with_no_text()
    {
        var doc = ValidDoc(TextLayer(text: null));
        Assert.Contains(New().Validate(doc), e => e.Contains("needs text"));
    }

    [Fact]
    public void Rejects_text_longer_than_the_cap()
    {
        var doc = ValidDoc(TextLayer(text: new string('a', 501)));
        Assert.Contains(New().Validate(doc), e => e.Contains("too long"));
    }

    [Fact]
    public void Rejects_an_image_layer_with_no_url()
    {
        var image = new PosterLayer("photo", "image", 0, 0, 100, 100, 0, 1, 1, Role: "photo", ImageUrl: null);
        Assert.Contains(New().Validate(ValidDoc(image)), e => e.Contains("your own uploads"));
    }

    [Fact]
    public void Rejects_an_external_image_url()
    {
        var image = new PosterLayer("photo", "image", 0, 0, 100, 100, 0, 1, 1, Role: "photo", ImageUrl: "https://evil.example.com/x.png");
        Assert.Contains(New().Validate(ValidDoc(image)), e => e.Contains("your own uploads"));
    }

    [Fact]
    public void Rejects_a_data_uri_image()
    {
        var image = new PosterLayer("photo", "image", 0, 0, 100, 100, 0, 1, 1, Role: "photo", ImageUrl: "data:image/png;base64,abc");
        Assert.Contains(New().Validate(ValidDoc(image)), e => e.Contains("your own uploads"));
    }

    [Fact]
    public void Accepts_an_image_url_under_the_configured_upload_path()
    {
        var image = new PosterLayer("photo", "image", 0, 0, 100, 100, 0, 1, 1, Role: "photo", ImageUrl: "https://cdn.example.com/uploads/2026/09/x.png", Fit: "cover");
        Assert.Empty(New().Validate(ValidDoc(image)));
    }

    [Fact]
    public void Rejects_a_shape_with_an_unknown_kind()
    {
        var shape = new PosterLayer("s1", "shape", 0, 0, 100, 100, 0, 1, 1, ShapeKind: "triangle");
        Assert.Contains(New().Validate(ValidDoc(shape)), e => e.Contains("unknown shape kind"));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Rejects_non_finite_geometry(double bad)
    {
        var doc = ValidDoc(TextLayer() with { X = bad });
        Assert.NotEmpty(New().Validate(doc));
    }

    [Fact]
    public void Rejects_zero_or_negative_size()
    {
        var doc = ValidDoc(TextLayer() with { Width = 0 });
        Assert.Contains(New().Validate(doc), e => e.Contains("width/height"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Rejects_opacity_outside_zero_to_one(double bad)
    {
        var doc = ValidDoc(TextLayer(opacity: bad));
        Assert.Contains(New().Validate(doc), e => e.Contains("opacity"));
    }

    [Fact]
    public void Rejects_a_background_image_that_is_not_our_own_upload()
    {
        var doc = ValidDoc() with { Background = new PosterDocBackground("image", ImageUrl: "https://evil.example.com/x.png") };
        Assert.Contains(New().Validate(doc), e => e.Contains("Background image"));
    }
}
