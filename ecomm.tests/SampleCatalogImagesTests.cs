using ecomm.api.Features.Ai;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// SampleCatalogImages.For used to reuse SectionPreviewExtractor's homepage-preview slice (hero + max 4
/// tiles ≈ 5 images/theme — built for theme-picker cards, not this), which meant a full catalog run
/// (up to 144 products) cycled through only ~10-15 curated images and repeated constantly. It now scans
/// every section of every template in each bundle instead. These pin the fix against the real
/// Themes/*.json bundles so a future bundle edit that shrinks the pool back down gets caught.
///
/// It also used to unconditionally widen every preset's pool with the "bazaar" theme "for variety" —
/// which meant a store type as narrow as "footwear" got a pool contaminated with bazaar's phones/
/// groceries/makeup/toys, and round-robin cycling handed most generated shoe products a completely
/// unrelated photo the moment it drifted past the few genuinely footwear-relevant images (confirmed
/// live: a real "Footwear / shoes" catalog run showed a red dress on "Cross Trainer Sneakers", a
/// grocery stall on "Dark Brown Cap Toe Leather Shoes", a makeup palette on "Patent Leather Formal
/// Shoes"). Fixed by no longer auto-appending bazaar — a preset that wants its variety (the "bazaar"
/// preset itself) already lists it explicitly in its own ThemeKeys.
/// </summary>
public class SampleCatalogImagesTests
{
    [Fact]
    public void For_returns_a_meaningfully_bigger_pool_than_the_old_hero_plus_4_tiles_cap()
    {
        var pool = SampleCatalogImages.For(new[] { "boutique", "noir" });   // Fashion preset's theme keys

        Assert.True(pool.Count >= 10, $"expected a wide pool (vs. the old ~5-image cap), got {pool.Count}");
    }

    [Fact]
    public void For_deduplicates_across_the_requested_themes()
    {
        var pool = SampleCatalogImages.For(new[] { "boutique", "noir" });

        Assert.Equal(pool.Count, pool.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void For_does_not_silently_widen_the_pool_with_bazaar()
    {
        // Regression test for the actual bug: adding "bazaar" explicitly must change the result —
        // if it didn't, that would mean bazaar is still being merged in behind the scenes.
        var withoutBazaar = SampleCatalogImages.For(new[] { "stride" });
        var withBazaarExplicit = SampleCatalogImages.For(new[] { "stride", "bazaar" });

        Assert.True(withBazaarExplicit.Count > withoutBazaar.Count,
            $"expected explicitly adding bazaar to grow the pool beyond the stride-only pool ({withoutBazaar.Count}), got {withBazaarExplicit.Count}");
    }

    [Fact]
    public void Footwear_preset_no_longer_pairs_with_the_general_fashion_theme()
    {
        // "boutique" is a general fashion theme (dresses, shirts, bags) — pairing it with "stride" (the
        // only genuinely shoe-focused theme) meant over half of footwear's pool was non-shoe imagery.
        // Confirmed live: a generated catalog put a red dress on "Heeled Loafers" and a clothing rack on
        // "Business Loafers". Footwear now round-robins through stride alone.
        var footwear = SampleCatalogPresets.Get("footwear")!;

        Assert.DoesNotContain("boutique", footwear.ThemeKeys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void For_an_unknown_theme_key_falls_back_to_the_single_placeholder()
    {
        // No bazaar safety net anymore — an unresolvable key legitimately has no real imagery to offer.
        var pool = SampleCatalogImages.For(new[] { "not-a-real-theme-key" });

        Assert.Single(pool);
        Assert.Contains("placehold.co", pool[0]);
    }

    [Fact]
    public void For_a_single_theme_preset_still_gets_a_real_pool_from_its_own_theme()
    {
        // "burgers" only maps to one theme (savor) — no bazaar padding anymore, but the theme's own
        // content (scanned across every section of every template) is still enough to be usable.
        var pool = SampleCatalogImages.For(new[] { "savor" });

        Assert.True(pool.Count >= 5, $"expected a usable pool from savor alone, got {pool.Count}");
        Assert.DoesNotContain(pool, url => url.Contains("placehold.co"));
    }
}
