using ecomm.api.Features.Ai;
using Xunit;

namespace ecomm.tests;

public class MigrationPresetsTests
{
    [Theory]
    [InlineData("shopify")]
    [InlineData("woocommerce")]
    [InlineData("wix")]
    [InlineData("zoho")]
    [InlineData("dukaan")]
    [InlineData("instamojo")]
    public void Every_preset_is_reachable_by_key(string key)
    {
        Assert.NotNull(MigrationPresets.Get(key));
    }

    [Fact]
    public void Get_is_case_insensitive_and_unknown_keys_return_null()
    {
        Assert.NotNull(MigrationPresets.Get("SHOPIFY"));
        Assert.Null(MigrationPresets.Get("some-unknown-platform"));
        Assert.Null(MigrationPresets.Get(null));
    }

    [Theory]
    [InlineData(new[] { "Handle", "Body (HTML)", "Variant SKU", "Variant Price", "Image Src" }, "shopify")]
    [InlineData(new[] { "Regular price", "Categories", "Images", "Short description", "In stock?" }, "woocommerce")]
    [InlineData(new[] { "handleId", "fieldType", "productImageUrl", "collection" }, "wix")]
    [InlineData(new[] { "Store Description", "Selling Price", "Qualifies For Returns", "Label Price" }, "zoho")]
    [InlineData(new[] { "HSN Code", "Product Name", "Selling Price" }, "dukaan")]
    public void A_real_header_row_is_detected_as_its_own_platform(string[] headers, string expectedKey)
    {
        var detected = MigrationPresets.Detect(headers);
        Assert.NotNull(detected);
        Assert.Equal(expectedKey, detected!.Key);
    }

    [Fact]
    public void A_header_row_with_only_one_signature_hit_is_not_detected()
    {
        // "Handle" alone is a Shopify signature column, but Detect() requires 2+ hits before committing.
        Assert.Null(MigrationPresets.Detect(new[] { "Handle", "SomeCompletelyUnknownColumn" }));
    }

    [Fact]
    public void An_unrecognised_header_row_is_not_detected_as_any_platform()
    {
        Assert.Null(MigrationPresets.Detect(new[] { "Widget Name", "Widget Cost", "Widget Notes" }));
    }

    [Theory]
    [InlineData("zoho", "Product Name", "name")]
    [InlineData("zoho", "Selling Price", "price")]
    [InlineData("zoho", "Label Price", "compareatprice")]
    [InlineData("zoho", "Store Description", "shortdescription")]
    [InlineData("dukaan", "Selling Price", "price")]
    [InlineData("dukaan", "Product Name", "name")]
    [InlineData("instamojo", "Price", "price")]
    [InlineData("instamojo", "Product Name", "name")]
    public void New_presets_map_their_key_columns_to_the_correct_target_field(string presetKey, string header, string expectedTarget)
    {
        var preset = MigrationPresets.Get(presetKey)!;
        Assert.True(preset.Map.TryGetValue(header, out var target));
        Assert.Equal(expectedTarget, target);
    }

    [Fact]
    public void Every_preset_map_lookup_is_case_insensitive()
    {
        foreach (var preset in MigrationPresets.All)
        {
            var anyHeader = preset.Map.Keys.First();
            Assert.True(preset.Map.ContainsKey(anyHeader.ToUpperInvariant()));
            Assert.True(preset.Map.ContainsKey(anyHeader.ToLowerInvariant()));
        }
    }
}
