using ecomm.api.Features.MarketingStudio;
using Xunit;

namespace ecomm.tests;

public class PosterSpecReaderTests
{
    [Fact]
    public void True_for_a_layers_v1_document()
    {
        Assert.True(PosterSpecReader.IsLayersDocument("{\"specVersion\":\"layers-v1\",\"other\":1}"));
    }

    [Fact]
    public void False_for_a_legacy_flat_spec_with_no_spec_version()
    {
        // What CreateAsync(PosterStudioRequest, ...) actually serializes today — no specVersion property.
        Assert.False(PosterSpecReader.IsLayersDocument("{\"kind\":\"org\",\"headline\":\"Big Sale\"}"));
    }

    [Fact]
    public void False_for_a_different_spec_version_string()
    {
        Assert.False(PosterSpecReader.IsLayersDocument("{\"specVersion\":\"layers-v2\"}"));
    }

    [Fact]
    public void False_for_genuinely_malformed_json()
    {
        Assert.False(PosterSpecReader.IsLayersDocument("{not json"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void False_for_null_or_blank(string? spec)
    {
        Assert.False(PosterSpecReader.IsLayersDocument(spec));
    }

    [Fact]
    public void False_when_spec_version_is_not_a_string()
    {
        Assert.False(PosterSpecReader.IsLayersDocument("{\"specVersion\":1}"));
    }

    [Fact]
    public void False_for_a_json_array_root()
    {
        Assert.False(PosterSpecReader.IsLayersDocument("[1,2,3]"));
    }
}
