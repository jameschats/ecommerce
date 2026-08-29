using ecomm.api.Features.Storefront;

namespace ecomm.api.Features.Ai;

/// <summary>A store-type starting point for the AI sample-catalog generator: an AI "brief" describing the
/// kind of store, plus the prebuilt-theme keys whose curated (live-verified) imagery seeds the photos.</summary>
public sealed record SampleCatalogPreset(string Key, string Label, string Brief, IReadOnlyList<string> ThemeKeys);

public static class SampleCatalogPresets
{
    public static readonly IReadOnlyList<SampleCatalogPreset> All = new[]
    {
        new SampleCatalogPreset("bazaar", "General store / bazaar", "a general online bazaar selling a broad mix of everyday products across several departments", new[] { "bazaar" }),
        new SampleCatalogPreset("electronics", "Electronics", "an electronics and gadgets store (phones, audio, wearables, accessories, home tech)", new[] { "ignition" }),
        new SampleCatalogPreset("fashion", "Fashion & apparel", "a fashion and apparel store with men's, women's and kids' clothing", new[] { "boutique", "noir" }),
        new SampleCatalogPreset("footwear", "Footwear / shoes", "a footwear store (sneakers, formal shoes, sandals, sports shoes)", new[] { "boutique", "bazaar" }),
        new SampleCatalogPreset("food", "Food & gourmet", "a gourmet food and snacks store (packaged foods, beverages, treats)", new[] { "savor" }),
        new SampleCatalogPreset("burgers", "Burger joint / QSR", "a burger and fast-food outlet menu (burgers, sides, beverages, combos)", new[] { "savor" }),
        new SampleCatalogPreset("grocery", "Grocery & daily needs", "a daily grocery store (staples, snacks, beverages, household essentials)", new[] { "fresh" }),
        new SampleCatalogPreset("beauty", "Beauty & personal care", "a beauty and personal-care store (skincare, makeup, haircare, fragrance)", new[] { "bloom", "lumiere" }),
        new SampleCatalogPreset("home", "Home & living", "a home and living store (decor, kitchen, storage, furnishings)", new[] { "haven" }),
        new SampleCatalogPreset("kids", "Kids & toys", "a kids and toys store (toys, games, learning, kids' essentials)", new[] { "sprout" }),
    };

    public static SampleCatalogPreset? Get(string? key) =>
        key is null ? null : All.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Curated product imagery for generated catalogs — pulled from the prebuilt themes' already-verified
/// hero/tile URLs (no new unverified links, no image generation). Photos are assigned round-robin so a
/// generated store looks real enough to "get the feel"; they're meant to be replaced by the merchant.
/// </summary>
public static class SampleCatalogImages
{
    private const string Fallback = "https://placehold.co/600x600?text=Product";

    /// <summary>The image pool for a set of theme keys (+ bazaar for variety), de-duplicated.</summary>
    public static IReadOnlyList<string> For(IEnumerable<string> themeKeys)
    {
        var byKey = PrebuiltThemeRegistry.Summaries.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        var keys = themeKeys.Append("bazaar");   // bazaar widens the pool
        var imgs = new List<string>();
        foreach (var k in keys)
            if (byKey.TryGetValue(k, out var s))
            {
                if (!string.IsNullOrWhiteSpace(s.HeroImage)) imgs.Add(s.HeroImage!);
                imgs.AddRange(s.TileImages.Where(u => !string.IsNullOrWhiteSpace(u)));
            }
        var pool = imgs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return pool.Count > 0 ? pool : new List<string> { Fallback };
    }
}
