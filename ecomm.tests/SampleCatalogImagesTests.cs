using ecomm.api.Features.Ai;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// SampleCatalogImages.For used to reuse SectionPreviewExtractor's homepage-preview slice (hero + max 4
/// tiles ≈ 5 images/theme — built for theme-picker cards, not this), which meant a full catalog run
/// (up to 144 products) cycled through only ~10-15 curated images and repeated constantly. It now scans
/// every section of every template in each bundle instead. These pin the fix against the real
/// Themes/*.json bundles so a future bundle edit that shrinks the pool back down gets caught.
/// </summary>
public class SampleCatalogImagesTests
{
    [Fact]
    public void For_returns_a_meaningfully_bigger_pool_than_the_old_hero_plus_4_tiles_cap()
    {
        var pool = SampleCatalogImages.For(new[] { "boutique", "noir" });   // Fashion preset's theme keys

        Assert.True(pool.Count >= 20, $"expected a wide pool, got {pool.Count}");
    }

    [Fact]
    public void For_deduplicates_across_the_requested_themes_and_the_always_appended_bazaar()
    {
        var pool = SampleCatalogImages.For(new[] { "boutique", "noir" });

        Assert.Equal(pool.Count, pool.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void For_still_returns_a_real_pool_when_the_requested_key_is_unknown_because_bazaar_is_always_appended()
    {
        var pool = SampleCatalogImages.For(new[] { "not-a-real-theme-key" });

        Assert.True(pool.Count > 1, "bazaar's own images should still come through");
        Assert.DoesNotContain(pool, url => url.Contains("placehold.co"));
    }

    [Fact]
    public void For_single_theme_preset_still_gets_a_usable_pool_via_the_bazaar_fallback()
    {
        // "burgers" only maps to one theme (savor) — the always-appended "bazaar" is what keeps a
        // single-theme preset from being stuck with just that one theme's handful of images.
        var pool = SampleCatalogImages.For(new[] { "savor" });

        Assert.True(pool.Count >= 15, $"expected bazaar to meaningfully widen a single-theme preset, got {pool.Count}");
    }
}
