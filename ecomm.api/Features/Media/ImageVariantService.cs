using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ecomm.api.Features.Media;

public interface IImageVariantService
{
    Task GenerateAsync(string originalUrl, Stream originalData, CancellationToken ct = default);
}

/// <summary>Generates resized WebP siblings for an uploaded image (400w/800w/1600w, skipping any width
/// larger than the original — never upscales) so the storefront can serve a right-sized image instead
/// of always downloading the full original. Deterministic naming (`{stem}-{w}w.webp`) is what lets the
/// frontend build a srcset with no DB/DTO changes — see ResponsiveImgDirective on the Angular side.
/// Best-effort: any failure (non-image content, corrupt file, unsupported format) is swallowed — the
/// original upload always succeeds regardless of this step.</summary>
public sealed class ImageVariantService(ILogger<ImageVariantService> logger, IMediaStorage storage) : IImageVariantService
{
    public static readonly int[] Widths = [400, 800, 1600];

    public async Task GenerateAsync(string originalUrl, Stream originalData, CancellationToken ct = default)
    {
        try
        {
            using var image = await Image.LoadAsync(originalData, ct);
            foreach (var width in Widths)
            {
                if (image.Width <= width) continue;
                using var resized = image.Clone(ctx => ctx.Resize(new ResizeOptions { Size = new Size(width, 0), Mode = ResizeMode.Max }));
                using var ms = new MemoryStream();
                await resized.SaveAsync(ms, new WebpEncoder(), ct);
                ms.Position = 0;
                await storage.SaveVariantAsync(originalUrl, $"-{width}w.webp", ms, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skipped responsive-image variants for {Url} (unsupported/corrupt image).", originalUrl);
        }
    }
}
