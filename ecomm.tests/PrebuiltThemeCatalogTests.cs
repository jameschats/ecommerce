using System.Text.Json;
using ecomm.api.Features.Storefront;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// Regression net for the embedded JSON theme bundles (Themes/*.json). Every future theme is authored
/// as pure data in that format, so these assertions are what catches a malformed or incomplete bundle
/// at test time — long before a deploy.
/// </summary>
public class PrebuiltThemeCatalogTests
{
    [Fact]
    public void Loads_all_bundles_with_unique_keys_in_stable_order()
    {
        Assert.Equal(15, PrebuiltThemeRegistry.All.Count);
        Assert.Equal(15, PrebuiltThemeRegistry.All.Select(t => t.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("minimal", PrebuiltThemeRegistry.All[0].Key);   // picker order starts at the safe default
    }

    [Fact]
    public void Every_bundle_has_the_four_core_templates()
    {
        foreach (var t in PrebuiltThemeRegistry.All)
            foreach (var key in new[] { "index", "announcement", "header", "footer" })
                Assert.Contains(t.Templates, tpl => tpl.TemplateKey == key);
    }

    [Fact]
    public void CatalogFit_is_a_known_value_on_every_bundle()
    {
        foreach (var t in PrebuiltThemeRegistry.All)
            Assert.Contains(t.CatalogFit, new[] { "Small", "Medium", "Large" });
    }

    [Fact]
    public void All_section_settings_and_blocks_are_valid_json()
    {
        foreach (var t in PrebuiltThemeRegistry.All)
            foreach (var tpl in t.Templates)
                foreach (var s in tpl.Sections)
                {
                    if (s.Settings is not null) JsonDocument.Parse(s.Settings).Dispose();
                    if (s.Blocks is not null) JsonDocument.Parse(s.Blocks).Dispose();
                }
    }

    [Fact]
    public void Bundle_pages_if_any_have_unique_non_home_slugs_and_valid_json()
    {
        // Regression net for the pages[] bundle field (R1 infrastructure) — no bundle authors pages yet
        // (that's R2), so this is currently vacuous, but guards every future bundle that does.
        foreach (var t in PrebuiltThemeRegistry.All)
        {
            Assert.Equal(t.Pages.Count, t.Pages.Select(p => p.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            foreach (var p in t.Pages)
            {
                Assert.False(string.Equals("home", p.Slug, StringComparison.OrdinalIgnoreCase));
                Assert.False(string.IsNullOrWhiteSpace(p.Title));
                foreach (var s in p.Sections)
                {
                    if (s.Settings is not null) JsonDocument.Parse(s.Settings).Dispose();
                    if (s.Blocks is not null) JsonDocument.Parse(s.Blocks).Dispose();
                }
            }
        }
    }

    [Fact]
    public void Summaries_extract_preview_material_and_new_tags()
    {
        var s = PrebuiltThemeRegistry.Summaries.First(x => x.Key == "ignition");
        Assert.False(string.IsNullOrEmpty(s.HeroImage));     // SampleCatalogImages depends on these
        Assert.NotEmpty(s.TileImages);
        Assert.Equal("Large", s.CatalogFit);
        Assert.NotEmpty(s.Features);
    }
}
