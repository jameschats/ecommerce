using System.Text;
using ecomm.api.Features.Media;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>Everything the renderer needs to lay out one poster from the brand kit + subject.
/// <paramref name="BackgroundImageUrl"/> is an AI-generated scene (optional, credit-metered, from
/// <see cref="PosterStudioService.GenerateBackgroundAsync"/>) that fills the whole frame behind the
/// text, for a far more creative result than the plain gradient; <paramref name="ProductImageUrl"/> is
/// the merchant's own product photo, used as a lighter-weight top-band fallback when there's no AI
/// background. The renderer, not the model, draws the headline/price/CTA — so they're always crisp and
/// accurate, which text-in-image generation is unreliable at.</summary>
public sealed record PosterSpec(
    string Kind, string Headline, decimal? Price, string Cta,
    string? CompanyName, bool IncludeName, bool IncludeLogo, string? LogoUrl, string? ProductImageUrl,
    string Primary, string Secondary, string Accent, string? Font, string? BackgroundImageUrl = null);

/// <summary>Renders a poster to markup. Today: self-contained SVG (no browser, no infra) — renders as a
/// real image in the admin and downloads cleanly. Rasterisation to PNG for social upload swaps in later
/// behind this same interface (render worker), with no caller change (plan §3.5/§3.6).</summary>
public interface IPosterRenderer
{
    Task<string> RenderSvgAsync(PosterSpec spec, CancellationToken ct = default);
}

/// <summary>
/// Brand-themed SVG poster (1080×1080). A gradient ground from the brand colours, the product/brand
/// image when it lives in our own media store (embedded as a data URI so it renders even as an
/// &lt;img&gt; source), a wrapped headline, an optional price badge, a CTA pill, the logo and company
/// name — all driven by the merchant's brand kit. Images that aren't ours (or fail to load) are simply
/// skipped, so a poster always renders.
/// </summary>
public sealed class SvgPosterRenderer(IMediaStorage media) : IPosterRenderer
{
    public async Task<string> RenderSvgAsync(PosterSpec spec, CancellationToken ct = default)
    {
        const int W = 1080, H = 1080;
        var font = XmlEscape(string.IsNullOrWhiteSpace(spec.Font) ? "Arial" : spec.Font!) + ", Arial, sans-serif";
        var primary = SafeColor(spec.Primary, "#111827");
        var accent = SafeColor(spec.Accent, "#2563eb");
        var darker = Darken(primary, 0.45);

        var logo = spec.IncludeLogo ? await DataUriAsync(spec.LogoUrl, ct) : null;
        var background = await DataUriAsync(spec.BackgroundImageUrl, ct);
        var photo = background is null ? await DataUriAsync(spec.ProductImageUrl, ct) : null;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{W}\" height=\"{H}\" viewBox=\"0 0 {W} {H}\">");
        sb.Append("<defs>");
        sb.Append($"<linearGradient id=\"bg\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"{primary}\"/><stop offset=\"1\" stop-color=\"{darker}\"/></linearGradient>");
        sb.Append($"<linearGradient id=\"scrim\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"{darker}\" stop-opacity=\"0\"/><stop offset=\"1\" stop-color=\"{darker}\" stop-opacity=\"0.92\"/></linearGradient>");
        sb.Append($"<linearGradient id=\"topScrim\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"{darker}\" stop-opacity=\"0.75\"/><stop offset=\"1\" stop-color=\"{darker}\" stop-opacity=\"0\"/></linearGradient>");
        sb.Append("<clipPath id=\"photo\"><rect x=\"0\" y=\"0\" width=\"1080\" height=\"600\"/></clipPath>");
        sb.Append("</defs>");
        sb.Append("<rect width=\"1080\" height=\"1080\" fill=\"url(#bg)\"/>");

        if (background is not null)
        {
            // AI-generated scene fills the whole frame; a bottom scrim keeps the headline/CTA legible
            // over any content, so the model never has to (unreliably) render the text itself.
            sb.Append($"<image href=\"{background}\" x=\"0\" y=\"0\" width=\"1080\" height=\"1080\" preserveAspectRatio=\"xMidYMid slice\"/>");
            sb.Append("<rect x=\"0\" y=\"520\" width=\"1080\" height=\"560\" fill=\"url(#scrim)\"/>");
            if (logo is not null || (spec.IncludeName && !string.IsNullOrWhiteSpace(spec.CompanyName)))
                sb.Append("<rect x=\"0\" y=\"0\" width=\"1080\" height=\"260\" fill=\"url(#topScrim)\"/>");
        }
        else if (photo is not null)
        {
            // Merchant's own product photo across the top when available; else a soft accent band.
            sb.Append($"<image href=\"{photo}\" x=\"0\" y=\"0\" width=\"1080\" height=\"600\" clip-path=\"url(#photo)\" preserveAspectRatio=\"xMidYMid slice\"/>");
            sb.Append("<rect x=\"0\" y=\"420\" width=\"1080\" height=\"180\" fill=\"url(#bg)\" opacity=\"0.55\"/>");
        }
        else
        {
            sb.Append($"<rect x=\"0\" y=\"0\" width=\"1080\" height=\"12\" fill=\"{accent}\"/>");
        }

        // Logo (embedded) top-left.
        if (logo is not null)
            sb.Append($"<image href=\"{logo}\" x=\"80\" y=\"72\" width=\"260\" height=\"120\" preserveAspectRatio=\"xMinYMid meet\"/>");
        else if (spec.IncludeName && !string.IsNullOrWhiteSpace(spec.CompanyName))
            sb.Append($"<text x=\"80\" y=\"140\" font-family=\"{font}\" font-size=\"42\" font-weight=\"700\" fill=\"#ffffff\">{XmlEscape(spec.CompanyName!)}</text>");

        // Headline — wrapped, bottom third.
        var lines = Wrap(spec.Headline, 18, 3);
        var startY = 720;
        for (var i = 0; i < lines.Count; i++)
            sb.Append($"<text x=\"80\" y=\"{startY + i * 92}\" font-family=\"{font}\" font-size=\"78\" font-weight=\"800\" fill=\"#ffffff\">{XmlEscape(lines[i])}</text>");

        var afterHeadline = startY + lines.Count * 92 + 20;

        // Price badge.
        if (spec.Price is { } price && price > 0)
        {
            sb.Append($"<rect x=\"80\" y=\"{afterHeadline}\" width=\"260\" height=\"84\" rx=\"14\" fill=\"{accent}\"/>");
            sb.Append($"<text x=\"210\" y=\"{afterHeadline + 56}\" font-family=\"{font}\" font-size=\"46\" font-weight=\"800\" fill=\"#ffffff\" text-anchor=\"middle\">₹{price:0}</text>");
            afterHeadline += 108;
        }

        // CTA pill bottom.
        var cta = XmlEscape(string.IsNullOrWhiteSpace(spec.Cta) ? "Shop Now" : spec.Cta);
        sb.Append($"<rect x=\"80\" y=\"940\" width=\"320\" height=\"92\" rx=\"46\" fill=\"{accent}\"/>");
        sb.Append($"<text x=\"240\" y=\"999\" font-family=\"{font}\" font-size=\"40\" font-weight=\"700\" fill=\"#ffffff\" text-anchor=\"middle\">{cta}</text>");

        // Company name bottom-right when the logo already used the name slot.
        if (spec.IncludeName && logo is not null && !string.IsNullOrWhiteSpace(spec.CompanyName))
            sb.Append($"<text x=\"1000\" y=\"999\" font-family=\"{font}\" font-size=\"34\" font-weight=\"600\" fill=\"#ffffff\" opacity=\"0.85\" text-anchor=\"end\">{XmlEscape(spec.CompanyName!)}</text>");

        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>Read one of our own media files and return a data: URI; null for remote/missing/unreadable.</summary>
    private async Task<string?> DataUriAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            await using var stream = await media.OpenReadAsync(url, ct);
            if (stream is null) return null;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            if (ms.Length == 0 || ms.Length > 4_000_000) return null;   // keep the SVG a sane size
            var mime = MimeFromUrl(url);
            return $"data:{mime};base64,{Convert.ToBase64String(ms.ToArray())}";
        }
        catch { return null; }
    }

    private static string MimeFromUrl(string url)
    {
        var u = url.ToLowerInvariant();
        if (u.EndsWith(".png")) return "image/png";
        if (u.EndsWith(".webp")) return "image/webp";
        if (u.EndsWith(".svg")) return "image/svg+xml";
        return "image/jpeg";
    }

    /// <summary>Greedy word-wrap to at most <paramref name="maxLines"/> lines of ~<paramref name="maxChars"/> chars.</summary>
    private static List<string> Wrap(string text, int maxChars, int maxLines)
    {
        var words = (text ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var cur = "";
        foreach (var w in words)
        {
            var candidate = cur.Length == 0 ? w : cur + " " + w;
            if (candidate.Length > maxChars && cur.Length > 0) { lines.Add(cur); cur = w; if (lines.Count == maxLines - 1) break; }
            else cur = candidate;
        }
        if (cur.Length > 0 && lines.Count < maxLines) lines.Add(cur);
        if (lines.Count == 0) lines.Add("");
        // If we truncated, add an ellipsis to the last line.
        if (words.Length > 0 && lines.Sum(l => l.Split(' ').Length) < words.Length && lines.Count > 0)
            lines[^1] = lines[^1].Length > maxChars - 1 ? lines[^1][..(maxChars - 1)] + "…" : lines[^1] + "…";
        return lines;
    }

    private static string XmlEscape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    private static string SafeColor(string? c, string fallback)
    {
        if (string.IsNullOrWhiteSpace(c)) return fallback;
        var t = c.Trim();
        if (t[0] != '#' || (t.Length != 4 && t.Length != 7)) return fallback;
        for (var i = 1; i < t.Length; i++) if (!Uri.IsHexDigit(t[i])) return fallback;
        return t;
    }

    /// <summary>Darken a #rrggbb / #rgb hex colour toward black by <paramref name="factor"/> (0–1).</summary>
    private static string Darken(string hex, double factor)
    {
        var h = hex.Length == 4 ? $"#{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}" : hex;
        int R = Convert.ToInt32(h.Substring(1, 2), 16), G = Convert.ToInt32(h.Substring(3, 2), 16), B = Convert.ToInt32(h.Substring(5, 2), 16);
        R = (int)(R * (1 - factor)); G = (int)(G * (1 - factor)); B = (int)(B * (1 - factor));
        return $"#{R:x2}{G:x2}{B:x2}";
    }
}
