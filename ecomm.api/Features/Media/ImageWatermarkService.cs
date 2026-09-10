using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ecomm.api.Features.Media;

public interface IImageWatermarkService
{
    /// <summary>
    /// Draws the shop's name in a faint, repeating diagonal pattern across the image, baked
    /// into the pixels so it survives a screenshot or a right-click save just as much as it
    /// survives someone finding the raw file URL in dev tools. Animated GIFs pass through
    /// unmodified — a fixed watermark would only ever land on their first frame.
    /// </summary>
    byte[] Apply(byte[] source, string contentType);
}

public sealed class ImageWatermarkService : IImageWatermarkService
{
    private const string Text = "Senthaamarai Press";

    // Bundled rather than read from SystemFonts: the production host has no fonts installed
    // at all, which would otherwise make every upload silently ship unwatermarked. Loaded
    // once and reused — SixLabors.Fonts.FontFamily is safe to share across calls.
    private static readonly Lazy<FontFamily?> BundledFamily = new(LoadBundledFont);

    private static FontFamily? LoadBundledFont()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Features", "Media", "Fonts", "Lato-Bold.ttf");
        if (!File.Exists(path)) return null;
        return new FontCollection().Add(path);
    }

    public byte[] Apply(byte[] source, string contentType)
    {
        if (string.Equals(contentType, "image/gif", StringComparison.OrdinalIgnoreCase)) return source;
        if (BundledFamily.Value is not { } family) return source; // Font file missing — ship unwatermarked rather than fail the upload.

        using var image = Image.Load<Rgba32>(source);

        var fontSize = FontSizeFor(image.Width);
        var font = family.CreateFont(fontSize, FontStyle.Bold);
        // A dark outline behind the white fill keeps the text visible on both dark artwork
        // and light backgrounds (a calendar image's caption strip is often plain white,
        // where a pure white watermark would otherwise vanish).
        var fill = Brushes.Solid(Color.White.WithAlpha(0.35f));
        var outline = Pens.Solid(Color.Black.WithAlpha(0.25f), Math.Max(1f, fontSize / 16f));
        var options = new RichTextOptions(font)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Spacing derived from the text's own rendered size, not the image's — a fixed
        // fraction of image dimensions crowded tiles together (adjacent repeats overlapping
        // into an unreadable smear) whenever the text came out wide relative to the canvas.
        var textSize = TextMeasurer.MeasureSize(Text, new TextOptions(font));
        var stepX = textSize.Width * 1.7f;
        var stepY = textSize.Height * 3.5f;
        var center = new PointF(image.Width / 2f, image.Height / 2f);
        // Half the canvas diagonal: drawing this far out from centre in every direction, before
        // rotating, guarantees the tiled rows still fully cover the corners after rotation.
        var half = MathF.Sqrt(image.Width * image.Width + image.Height * image.Height) / 2f;
        var rotation = Matrix3x2.CreateRotation(-25f * MathF.PI / 180f, center);

        image.Mutate(ctx =>
        {
            ctx.SetDrawingTransform(rotation);
            var row = 0;
            for (var y = center.Y - half; y < center.Y + half; y += stepY, row++)
            {
                var offset = row % 2 == 0 ? 0 : stepX / 2;
                for (var x = center.X - half + offset; x < center.X + half; x += stepX)
                {
                    options.Origin = new PointF(x, y);
                    ctx.DrawText(options, Text, fill, outline);
                }
            }
        });

        using var output = new MemoryStream();
        image.Save(output, EncoderFor(contentType));
        return output.ToArray();
    }

    private static float FontSizeFor(int imageWidth) => Math.Clamp(imageWidth / 14f, 14f, 48f);

    private static IImageEncoder EncoderFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => new PngEncoder(),
        "image/webp" => new WebpEncoder(),
        _ => new JpegEncoder { Quality = 90 },
    };
}
