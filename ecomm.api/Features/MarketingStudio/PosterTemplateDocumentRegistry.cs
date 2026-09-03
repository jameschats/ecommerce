using System.Text.Json;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>Metadata about one poster template — id/name/description/category for the Browse gallery,
/// same shape <see cref="PosterTemplateInfo"/> already exposed, sourced from here instead of a
/// hardcoded array so there's one place templates are defined, not two that can drift.</summary>
public sealed record PosterTemplateMeta(string Id, string Name, string Description, string Category, bool UsesPhoto);

/// <summary>
/// The poster template library — hand-authored starter layer documents a merchant picks from,
/// Canva-style, grown one at a time. Templates are pure DATA (a <see cref="PosterDocument"/> per
/// format), authored as embedded <c>PosterTemplates/*.json</c> bundle files — the exact same
/// pure-data-JSON, embedded-resource convention <see cref="Storefront.PrebuiltThemeRegistry"/> already
/// uses for storefront themes. Colour fields may contain the placeholder tokens <c>{primary}</c>,
/// <c>{primaryDark}</c>, <c>{secondary}</c>, <c>{accent}</c> and <c>{font}</c> — resolved against the
/// tenant's brand kit by <see cref="PosterStudioService"/> when a starter document is requested, never
/// here (this registry has no tenant/DB access, matching the storefront registry's own separation of
/// pure data from tenant-specific resolution). A malformed bundle throws with its resource name on
/// first access — caught by <c>PosterTemplateDocumentRegistryTests</c> long before a deploy.
/// </summary>
public static class PosterTemplateDocumentRegistry
{
    private sealed record BundleFile(
        string Id, int SortOrder, string Name, string Description, string Category, bool UsesPhoto,
        Dictionary<string, BundleFormat> Formats);
    private sealed record BundleFormat(int Width, int Height, PosterDocBackground Background, List<PosterLayer> Layers);

    private sealed record LoadedTemplate(PosterTemplateMeta Meta, int SortOrder, IReadOnlyDictionary<string, PosterDocument> ByFormat);

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly Lazy<IReadOnlyList<LoadedTemplate>> Loaded = new(LoadAll);

    public static IReadOnlyList<PosterTemplateMeta> All => Loaded.Value.Select(t => t.Meta).ToList();

    /// <summary>The starter document for one template at one format, colours still as placeholder
    /// tokens — the caller (<see cref="PosterStudioService"/>) resolves those against the brand kit.
    /// Null if the template or format id isn't known.</summary>
    public static PosterDocument? StarterDocument(string templateId, string formatId)
    {
        var t = Loaded.Value.FirstOrDefault(x => string.Equals(x.Meta.Id, templateId, StringComparison.OrdinalIgnoreCase));
        if (t is null) return null;
        return t.ByFormat.GetValueOrDefault(formatId) ?? t.ByFormat.GetValueOrDefault("square");
    }

    private static IReadOnlyList<LoadedTemplate> LoadAll()
    {
        var asm = typeof(PosterTemplateDocumentRegistry).Assembly;
        var names = asm.GetManifestResourceNames()
            .Where(n => n.Contains(".PosterTemplates.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        var loaded = new List<LoadedTemplate>();
        foreach (var name in names)
        {
            try
            {
                using var stream = asm.GetManifestResourceStream(name)!;
                var file = JsonSerializer.Deserialize<BundleFile>(stream, JsonOpts)
                    ?? throw new JsonException("empty document");
                var byFormat = file.Formats.ToDictionary(
                    kv => kv.Key,
                    kv => new PosterDocument("layers-v1", new PosterDocFormat(kv.Value.Width, kv.Value.Height),
                        kv.Value.Background, kv.Value.Layers, file.Id),
                    StringComparer.OrdinalIgnoreCase);
                loaded.Add(new LoadedTemplate(
                    new PosterTemplateMeta(file.Id, file.Name, file.Description, file.Category, file.UsesPhoto),
                    file.SortOrder, byFormat));
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException($"Invalid poster template bundle '{name}': {ex.Message}", ex);
            }
        }
        return loaded.OrderBy(x => x.SortOrder).ThenBy(x => x.Meta.Name, StringComparer.Ordinal).ToList();
    }
}
