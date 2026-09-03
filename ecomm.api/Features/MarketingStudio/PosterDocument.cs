namespace ecomm.api.Features.MarketingStudio;

/// <summary>
/// The persisted shape of a freeform, layer-based poster — deliberately a thin, explicit, versioned
/// schema rather than a raw dump of whatever the client-side canvas library's own serialization looks
/// like. Raw third-party canvas JSON is unstable across library upgrades, full of library-internal
/// noise, and can't be validated or reasoned about server-side. <see cref="SpecVersion"/> is the
/// explicit discriminator (see <see cref="PosterSpecReader"/>) that tells a new poster apart from the
/// old flat-field <see cref="PosterStudioRequest"/> shape still stored on posters made before this
/// existed — never inferred by whether deserialization happens to succeed.
/// </summary>
public sealed record PosterDocument(
    string SpecVersion,                 // always "layers-v1" for this shape
    PosterDocFormat Format,
    PosterDocBackground Background,
    IReadOnlyList<PosterLayer> Layers,
    string? TemplateId = null,          // which starter template this began from, if any
    string? Kind = null,                // "org" | "product" — preserved for AutoFillDraft/product linkage
    long? ProductId = null);

public sealed record PosterDocFormat(int Width, int Height);

/// <summary><paramref name="Type"/> is "color" or "image" — the ground beneath every layer. Kept
/// separate from the layer list because it's always present and isn't meant to be independently
/// selected/deleted the way a real layer is.</summary>
public sealed record PosterDocBackground(string Type, string? Color = null, string? ImageUrl = null);

/// <summary>One selectable, movable, resizable element on the canvas. Every field below is optional on
/// this shared record — simplest to (de)serialize with System.Text.Json without a polymorphic
/// converter — but each <paramref name="Type"/> only ever populates its own subset; that subset is
/// enforced by <see cref="PosterDocumentValidator"/>, not by the type system.
/// <paramref name="Role"/> lets AI actions (auto-fill, suggest headline, generate background) target a
/// layer by intent ("the headline", "the photo") instead of a client-generated id that may no longer
/// exist after the merchant deletes or duplicates layers.</summary>
public sealed record PosterLayer(
    string Id,
    string Type,                        // "text" | "image" | "shape"
    double X, double Y,
    double Width, double Height,
    double Rotation,
    double Opacity,
    int ZIndex,
    string? Role = null,                // "headline" | "price" | "cta" | "logo" | "photo" | "background" | null

    // ---- text ----
    string? Text = null,
    string? FontFamily = null,
    double? FontSize = null,
    string? FontWeight = null,          // "400" | "700" | "900" ...
    string? FontStyle = null,           // "normal" | "italic"
    string? TextAlign = null,           // "left" | "center" | "right"
    string? Color = null,
    double? LineHeight = null,
    double? LetterSpacing = null,

    // ---- image ----
    string? ImageUrl = null,            // must be our own storage — never an external URL or data: URI
    string? Fit = null,                 // "cover" | "contain"
    double? CornerRadius = null,

    // ---- shape ----
    string? ShapeKind = null,           // "rect" | "ellipse" | "line"
    string? Fill = null,
    string? Stroke = null,
    double? StrokeWidth = null);
