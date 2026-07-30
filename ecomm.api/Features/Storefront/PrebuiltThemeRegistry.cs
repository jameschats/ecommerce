using System.Text.Json;

namespace ecomm.api.Features.Storefront;

/// <summary>One section in a prebuilt theme template: a type + ready-made settings/blocks JSON.</summary>
public sealed record PrebuiltSection(string Type, string? Title, string? Settings, string? Blocks);

/// <summary>One page-type template (or header/footer/announcement zone) within a prebuilt theme.</summary>
public sealed record PrebuiltTemplate(string TemplateKey, IReadOnlyList<PrebuiltSection> Sections);

/// <summary>A content page a theme bundle can seed at install time (e.g. "Our Story", "Shipping &amp; Returns").</summary>
public sealed record PrebuiltPage(string Title, string Slug, IReadOnlyList<PrebuiltSection> Sections);

/// <summary>A full free prebuilt theme bundle: global settings + a set of templates + optional content pages.</summary>
public sealed record PrebuiltTheme(
    string Key, string Name, string Category, string CatalogFit, IReadOnlyList<string> Features,
    string Description, IReadOnlyDictionary<string, string> Settings, IReadOnlyList<PrebuiltTemplate> Templates,
    IReadOnlyList<PrebuiltPage> Pages);

/// <summary>
/// Install-picker summary. Carries enough of the bundle to render a Shopify-style mini-preview
/// (real hero image + heading + tile imagery) so a card shows what the store will look like.
/// </summary>
public sealed record PrebuiltThemeSummary(
    string Key, string Name, string Category, string CatalogFit, IReadOnlyList<string> Features,
    string Description, string PrimaryColor, string SecondaryColor, string Font,
    string HeadingFont, string Radius, string? HeroImage, string? HeroHeading, IReadOnlyList<string> TileImages);

/// <summary>
/// The catalog of free prebuilt themes a merchant can install into their library (S6) — our answer to a
/// paid theme store. Themes are pure DATA (settings + section JSON; all rendering code lives in the
/// platform), authored as embedded <c>Themes/*.json</c> bundle files — the same format a future
/// super-admin upload/import would accept. Installing copies a bundle into the tenant's library as a
/// Draft to preview then publish. A malformed bundle throws on first access with its resource name —
/// caught by the test suite (PrebuiltThemeCatalogTests) long before a deploy.
/// </summary>
public static class PrebuiltThemeRegistry
{
    // ---- bundle-file schema (what Themes/*.json deserialize into; settings/blocks are real nested
    //      JSON for authorability, compact-serialized to strings on load so everything downstream —
    //      the verbatim install copy, PreviewOf — keeps operating on strings exactly as before) ----
    private sealed record BundleFile(
        string Key, int SortOrder, string Name, string Category, string CatalogFit,
        List<string>? Features, string Description,
        Dictionary<string, string> Settings, List<BundleTemplate> Templates, List<BundlePage>? Pages);
    private sealed record BundleTemplate(string TemplateKey, List<BundleSection> Sections);
    private sealed record BundlePage(string Title, string Slug, List<BundleSection> Sections);
    private sealed record BundleSection(string Type, string? Title, JsonElement? Settings, JsonElement? Blocks);

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly Lazy<IReadOnlyList<PrebuiltTheme>> Loaded = new(LoadAll);

    public static IReadOnlyList<PrebuiltTheme> All => Loaded.Value;

    private static IReadOnlyList<PrebuiltTheme> LoadAll()
    {
        var asm = typeof(PrebuiltThemeRegistry).Assembly;
        var names = asm.GetManifestResourceNames()
            .Where(n => n.Contains(".Themes.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        var loaded = new List<(int SortOrder, PrebuiltTheme Theme)>();
        foreach (var name in names)
        {
            try
            {
                using var stream = asm.GetManifestResourceStream(name)!;
                var file = JsonSerializer.Deserialize<BundleFile>(stream, JsonOpts)
                    ?? throw new JsonException("empty document");
                loaded.Add((file.SortOrder, ToTheme(file)));
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException($"Invalid prebuilt theme bundle '{name}': {ex.Message}", ex);
            }
        }
        return loaded.OrderBy(x => x.SortOrder).ThenBy(x => x.Theme.Name, StringComparer.Ordinal)
            .Select(x => x.Theme).ToList();
    }

    private static PrebuiltTheme ToTheme(BundleFile f) => new(
        f.Key, f.Name, f.Category, f.CatalogFit, f.Features ?? [], f.Description, f.Settings,
        f.Templates.Select(t => new PrebuiltTemplate(
            t.TemplateKey,
            t.Sections.Select(ToSection).ToList()
        )).ToList(),
        (f.Pages ?? []).Select(p => new PrebuiltPage(p.Title, p.Slug, p.Sections.Select(ToSection).ToList())).ToList());

    private static PrebuiltSection ToSection(BundleSection s) => new(s.Type, s.Title, Compact(s.Settings), Compact(s.Blocks));

    /// <summary>Nested authored JSON → the compact string form the theme tables store.</summary>
    private static string? Compact(JsonElement? el)
    {
        if (el is null) return null;
        var e = el.Value;
        return e.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? null : JsonSerializer.Serialize(e);
    }

    public static IReadOnlyList<PrebuiltThemeSummary> Summaries =>
        All.Select(t =>
        {
            var (heroImage, heroHeading, tiles) = PreviewOf(t);
            return new PrebuiltThemeSummary(
                t.Key, t.Name, t.Category, t.CatalogFit, t.Features, t.Description,
                t.Settings.GetValueOrDefault("PrimaryColor", "#111827"),
                t.Settings.GetValueOrDefault("SecondaryColor", "#6b7280"),
                t.Settings.GetValueOrDefault("Font", "Inter"),
                t.Settings.GetValueOrDefault("HeadingFont", "Inter"),
                t.Settings.GetValueOrDefault("Radius", "soft"),
                heroImage, heroHeading, tiles);
        }).ToList();

    /// <summary>Extract mini-preview material (hero image + heading, up to 4 tile images) from a bundle's index.</summary>
    private static (string? heroImage, string? heroHeading, IReadOnlyList<string> tiles) PreviewOf(PrebuiltTheme t)
    {
        string? heroImage = null, heroHeading = null;
        var tiles = new List<string>();
        var index = t.Templates.FirstOrDefault(x => x.TemplateKey == "index");
        if (index is null) return (null, null, tiles);

        foreach (var s in index.Sections)
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
                else if ((s.Type == "TileGrid" || s.Type == "PromoTiles") && tiles.Count < 4)
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
            catch { /* preview extraction is best-effort — a malformed block never breaks the picker */ }
        }
        return (heroImage, heroHeading, tiles);
    }

    public static PrebuiltTheme? Get(string key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
}
