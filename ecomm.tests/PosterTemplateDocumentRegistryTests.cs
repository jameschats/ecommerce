using ecomm.api.Features.MarketingStudio;
using Xunit;

namespace ecomm.tests;

public class PosterTemplateDocumentRegistryTests
{
    [Fact]
    public void Both_shipped_templates_load_without_throwing()
    {
        var all = PosterTemplateDocumentRegistry.All;
        Assert.Contains(all, t => t.Id == "bold-medallion");
        Assert.Contains(all, t => t.Id == "minimal-type");
    }

    [Fact]
    public void Bold_medallion_declares_it_uses_a_photo_and_minimal_type_does_not()
    {
        var all = PosterTemplateDocumentRegistry.All;
        Assert.True(all.First(t => t.Id == "bold-medallion").UsesPhoto);
        Assert.False(all.First(t => t.Id == "minimal-type").UsesPhoto);
    }

    [Theory]
    [InlineData("bold-medallion")]
    [InlineData("minimal-type")]
    public void Every_template_has_a_headline_and_a_cta_layer_at_every_format(string templateId)
    {
        foreach (var format in new[] { "square", "story" })
        {
            var doc = PosterTemplateDocumentRegistry.StarterDocument(templateId, format);
            Assert.NotNull(doc);
            Assert.Contains(doc!.Layers, l => l.Role == "headline");
            Assert.Contains(doc.Layers, l => l.Role == "cta");
            Assert.Equal("layers-v1", doc.SpecVersion);
        }
    }

    [Fact]
    public void Bold_medallion_has_a_photo_layer_minimal_type_does_not()
    {
        Assert.Contains(PosterTemplateDocumentRegistry.StarterDocument("bold-medallion", "square")!.Layers, l => l.Role == "photo");
        Assert.DoesNotContain(PosterTemplateDocumentRegistry.StarterDocument("minimal-type", "square")!.Layers, l => l.Role == "photo");
    }

    [Fact]
    public void Story_format_is_taller_than_square_at_the_same_width()
    {
        var square = PosterTemplateDocumentRegistry.StarterDocument("bold-medallion", "square")!;
        var story = PosterTemplateDocumentRegistry.StarterDocument("bold-medallion", "story")!;
        Assert.Equal(square.Format.Width, story.Format.Width);
        Assert.True(story.Format.Height > square.Format.Height);
    }

    [Fact]
    public void Unknown_format_falls_back_to_square()
    {
        var doc = PosterTemplateDocumentRegistry.StarterDocument("bold-medallion", "not-a-format");
        Assert.NotNull(doc);
        Assert.Equal(1080, doc!.Format.Height);
    }

    [Fact]
    public void Unknown_template_id_returns_null()
    {
        Assert.Null(PosterTemplateDocumentRegistry.StarterDocument("not-a-template", "square"));
    }

    [Fact]
    public void Colour_fields_carry_unresolved_placeholder_tokens()
    {
        // Resolution against the brand kit happens in PosterStudioService, not here — the registry has
        // no tenant/DB access, matching PrebuiltThemeRegistry's own pure-data/no-resolution split.
        var doc = PosterTemplateDocumentRegistry.StarterDocument("bold-medallion", "square")!;
        Assert.Equal("{primary}", doc.Background.Color);
    }
}
