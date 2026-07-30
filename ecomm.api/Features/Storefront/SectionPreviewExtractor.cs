using System.Text.Json;

namespace ecomm.api.Features.Storefront;

/// <summary>
/// Shared "what does this theme's homepage look like" extraction — pulls a hero image/heading and up to
/// 4 tile images out of a set of index-template sections by parsing their Blocks JSON. Used both over a
/// prebuilt bundle's static section list (<see cref="PrebuiltThemeRegistry"/>, install-picker cards) and
/// a real theme's live <c>ThemeSection</c> rows (<see cref="ThemeLibraryService"/>, the theme library
/// grid) — same logic, different source, so "what you'll get" and "what you actually have" previews never
/// drift apart.
/// </summary>
public static class SectionPreviewExtractor
{
    public static (string? HeroImage, string? HeroHeading, IReadOnlyList<string> TileImages) Extract(
        IEnumerable<(string Type, string? Blocks)> indexSections)
    {
        string? heroImage = null, heroHeading = null;
        var tiles = new List<string>();

        foreach (var s in indexSections)
        {
            if (string.IsNullOrEmpty(s.Blocks)) continue;
            try
            {
                if (s.Type == "Hero" && (heroImage is null || heroHeading is null))
                {
                    using var doc = JsonDocument.Parse(s.Blocks);
                    foreach (var b in doc.RootElement.EnumerateArray())
                    {
                        var img = b.TryGetProperty("image", out var i) ? i.GetString() : null;
                        if (heroHeading is null && b.TryGetProperty("heading", out var h)) heroHeading = h.GetString();
                        if (heroImage is null && !string.IsNullOrEmpty(img)) heroImage = img;
                    }
                }
                else if ((s.Type is "TileGrid" or "PromoTiles" or "Collage") && tiles.Count < 4)
                {
                    using var doc = JsonDocument.Parse(s.Blocks);
                    foreach (var b in doc.RootElement.EnumerateArray())
                    {
                        if (tiles.Count >= 4) break;
                        var img = b.TryGetProperty("image", out var i) ? i.GetString() : null;
                        if (!string.IsNullOrEmpty(img)) tiles.Add(img!);
                    }
                }
            }
            catch { /* preview extraction is best-effort — a malformed block never breaks a preview */ }
        }

        return (heroImage, heroHeading, tiles);
    }
}
