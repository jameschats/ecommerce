using ecomm.api.Features.Media;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>Rejects a freeform poster document before it's persisted. This is the main backend test
/// surface for the canvas editor — actual rendering happens client-side, so the server's job is purely
/// to make sure what it's about to store is well-formed and safe (no arbitrary external image URLs, no
/// runaway layer counts/text lengths, no non-finite geometry).</summary>
public interface IPosterDocumentValidator
{
    IReadOnlyList<string> Validate(PosterDocument doc);
}

public sealed class PosterDocumentValidator(IPosterRenderer renderer, IOptions<MediaOptions> mediaOptions) : IPosterDocumentValidator
{
    private const int MaxLayers = 40;
    private const int MaxTextLength = 500;
    private static readonly HashSet<string> LayerTypes = new(StringComparer.Ordinal) { "text", "image", "shape" };
    private static readonly HashSet<string> ShapeKinds = new(StringComparer.Ordinal) { "rect", "ellipse", "line" };
    private static readonly HashSet<string> Fits = new(StringComparer.Ordinal) { "cover", "contain" };

    public IReadOnlyList<string> Validate(PosterDocument doc)
    {
        var errors = new List<string>();

        if (doc.SpecVersion != "layers-v1")
            errors.Add("Unknown document version.");

        if (!renderer.AvailableFormats.Any(f => f.Width == doc.Format.Width && f.Height == doc.Format.Height))
            errors.Add("Unknown canvas format.");

        if (doc.Background.Type is not ("color" or "image"))
            errors.Add("Unknown background type.");
        else if (doc.Background.Type == "image" && !IsOwnUpload(doc.Background.ImageUrl))
            errors.Add("Background image must be one of your own uploads.");

        if (doc.Layers.Count == 0)
            errors.Add("A poster needs at least one layer.");
        else if (doc.Layers.Count > MaxLayers)
            errors.Add($"Too many layers (max {MaxLayers}).");

        foreach (var layer in doc.Layers)
            ValidateLayer(layer, errors);

        return errors;
    }

    private void ValidateLayer(PosterLayer l, List<string> errors)
    {
        if (!LayerTypes.Contains(l.Type)) { errors.Add($"Layer '{l.Id}': unknown type."); return; }

        if (!IsFinite(l.X) || !IsFinite(l.Y) || !IsFinite(l.Rotation))
            errors.Add($"Layer '{l.Id}': position/rotation must be finite.");
        if (!IsFinite(l.Width) || !IsFinite(l.Height) || l.Width <= 0 || l.Height <= 0)
            errors.Add($"Layer '{l.Id}': width/height must be positive and finite.");
        if (!IsFinite(l.Opacity) || l.Opacity < 0 || l.Opacity > 1)
            errors.Add($"Layer '{l.Id}': opacity must be between 0 and 1.");

        switch (l.Type)
        {
            case "text":
                if (string.IsNullOrEmpty(l.Text))
                    errors.Add($"Layer '{l.Id}': a text layer needs text.");
                else if (l.Text.Length > MaxTextLength)
                    errors.Add($"Layer '{l.Id}': text is too long (max {MaxTextLength} characters).");
                break;
            case "image":
                if (!IsOwnUpload(l.ImageUrl))
                    errors.Add($"Layer '{l.Id}': image must be one of your own uploads.");
                if (l.Fit is not null && !Fits.Contains(l.Fit))
                    errors.Add($"Layer '{l.Id}': unknown fit.");
                break;
            case "shape":
                if (l.ShapeKind is null || !ShapeKinds.Contains(l.ShapeKind))
                    errors.Add($"Layer '{l.Id}': unknown shape kind.");
                break;
        }
    }

    /// <summary>An <see cref="PosterLayer.ImageUrl"/>/<see cref="PosterDocBackground.ImageUrl"/> must
    /// point at our own upload storage (matched by the configured request path, e.g. "/uploads") —
    /// never an arbitrary external URL or a <c>data:</c> URI baked straight into the document.</summary>
    private bool IsOwnUpload(string? url) =>
        !string.IsNullOrWhiteSpace(url) && !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
        && url.Contains(mediaOptions.Value.RequestPath, StringComparison.Ordinal);

    private static bool IsFinite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
}
