using System.Text.Json;
using ecomm.api.Features.Storefront;

namespace ecomm.api.Features.Ai;

/// <summary>A store-type starting point for the AI sample-catalog generator: an AI "brief" describing the
/// kind of store, plus the prebuilt-theme keys whose curated (live-verified) imagery seeds the photos.</summary>
public sealed record SampleCatalogPreset(string Key, string Label, string Brief, IReadOnlyList<string> ThemeKeys);

public static class SampleCatalogPresets
{
    public static readonly IReadOnlyList<SampleCatalogPreset> All = new[]
    {
        new SampleCatalogPreset("bazaar", "General store / bazaar", "a general online bazaar selling a broad mix of everyday products across several departments", new[] { "bazaar", "emporium" }),
        new SampleCatalogPreset("electronics", "Electronics", "an electronics and gadgets store (phones, audio, wearables, accessories, home tech)", new[] { "ignition", "pulse" }),
        new SampleCatalogPreset("fashion", "Fashion & apparel", "a fashion and apparel store with men's, women's and kids' clothing", new[] { "boutique", "noir" }),
        new SampleCatalogPreset("footwear", "Footwear / shoes", "a footwear store (sneakers, formal shoes, sandals, sports shoes)", new[] { "stride", "boutique" }),
        new SampleCatalogPreset("food", "Food & gourmet", "a gourmet food and snacks store (packaged foods, beverages, treats)", new[] { "savor", "roast" }),
        new SampleCatalogPreset("burgers", "Burger joint / QSR", "a burger and fast-food outlet menu (burgers, sides, beverages, combos)", new[] { "savor" }),
        new SampleCatalogPreset("grocery", "Grocery & daily needs", "a daily grocery store (staples, snacks, beverages, household essentials)", new[] { "fresh", "harvest" }),
        new SampleCatalogPreset("beauty", "Beauty & personal care", "a beauty and personal-care store (skincare, makeup, haircare, fragrance)", new[] { "bloom", "lumiere" }),
        new SampleCatalogPreset("home", "Home & living", "a home and living store (decor, kitchen, storage, furnishings)", new[] { "haven", "fjord" }),
        new SampleCatalogPreset("kids", "Kids & toys", "a kids and toys store (toys, games, learning, kids' essentials)", new[] { "sprout", "bubble" }),
    };

    public static SampleCatalogPreset? Get(string? key) =>
        key is null ? null : All.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Curated product imagery for generated catalogs — pulled from the prebuilt themes' already-verified
/// image URLs (no new unverified links, no image generation). Photos are assigned round-robin so a
/// generated store looks real enough to "get the feel"; they're meant to be replaced by the merchant.
/// </summary>
public static class SampleCatalogImages
{
    private const string Fallback = "https://placehold.co/600x600?text=Product";

    /// <summary>
    /// The image pool for a set of theme keys, de-duplicated. Scans every section of every template in
    /// each bundle — not just the homepage hero/tile preview (<see cref="SectionPreviewExtractor"/>,
    /// capped at 5 images/theme for the theme-picker card) — so a full catalog run (up to 144 products)
    /// has a meaningfully bigger pool to round-robin through instead of repeating the same handful of
    /// photos. Deliberately does NOT widen every preset's pool with the "bazaar" theme (it did once) —
    /// bazaar's imagery spans every category, so mixing it into e.g. "footwear"'s pool meant most
    /// generated shoe products got a random phone/grocery/makeup photo instead of a shoe photo once
    /// round-robin cycled past the few genuinely footwear-relevant images. A preset that wants bazaar's
    /// variety (the "bazaar" preset itself) already lists it explicitly in its own ThemeKeys.
    /// </summary>
    public static IReadOnlyList<string> For(IEnumerable<string> themeKeys)
    {
        var keys = themeKeys.Distinct(StringComparer.OrdinalIgnoreCase);
        var imgs = new List<string>();
        foreach (var k in keys)
        {
            var theme = PrebuiltThemeRegistry.Get(k);
            if (theme is null) continue;
            foreach (var template in theme.Templates)
                foreach (var section in template.Sections)
                    CollectImageUrls(section.Blocks, imgs);
        }
        var pool = imgs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return pool.Count > 0 ? pool : new List<string> { Fallback };
    }

    /// <summary>Recursively collects every string value under an "image" property, anywhere in the JSON.</summary>
    private static void CollectImageUrls(string? blocksJson, List<string> into)
    {
        if (string.IsNullOrWhiteSpace(blocksJson)) return;
        try
        {
            using var doc = JsonDocument.Parse(blocksJson);
            Walk(doc.RootElement, into);
        }
        catch (JsonException) { /* malformed block JSON — skip, never break catalog generation */ }
    }

    private static void Walk(JsonElement el, List<string> into)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    if (prop.NameEquals("image") && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var v = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(v)) into.Add(v!);
                    }
                    else Walk(prop.Value, into);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray()) Walk(item, into);
                break;
        }
    }
}
