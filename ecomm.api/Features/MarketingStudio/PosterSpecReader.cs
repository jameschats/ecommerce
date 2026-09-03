using System.Text.Json;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>Tells a new layer-based poster spec apart from an old flat-field one — deliberately by
/// checking for an explicit marker property, not by attempting to deserialize into one shape and
/// catching failure (that was the old, accidental discriminator: any JSON exception meant "must be
/// legacy," which is fragile and doesn't distinguish "legacy" from "genuinely corrupt").</summary>
public static class PosterSpecReader
{
    public const string LayersV1 = "layers-v1";

    /// <summary>True only when <paramref name="spec"/> is well-formed JSON with a top-level
    /// <c>"specVersion": "layers-v1"</c> property. Legacy <see cref="PosterStudioRequest"/> specs never
    /// had this property, so they always return false here.</summary>
    public static bool IsLayersDocument(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return false;
        try
        {
            using var doc = JsonDocument.Parse(spec);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("specVersion", out var v)
                && v.ValueKind == JsonValueKind.String
                && v.GetString() == LayersV1;
        }
        catch (JsonException) { return false; }
    }
}
