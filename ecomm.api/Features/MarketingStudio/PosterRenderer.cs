using System.Text;
using ecomm.api.Features.Media;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>Everything a template needs to lay out one poster from the brand kit + subject.
/// <paramref name="TemplateId"/> selects which hand-authored layout renders it (see
/// <see cref="IPosterRenderer.AvailableTemplates"/>) — a merchant picks a template the way they'd pick
/// one in Canva; the same data just flows into a different, deliberately-designed composition.
/// <paramref name="Format"/> selects the canvas size (see <see cref="IPosterRenderer.AvailableFormats"/>)
/// — every template must hold up at every format, not just the square default.
/// <paramref name="BackgroundImageUrl"/> is an AI-generated scene (optional, credit-metered, from
/// <see cref="PosterStudioService.GenerateBackgroundAsync"/>); <paramref name="ProductImageUrl"/> is the
/// merchant's own product photo — templates that use a photo treat either the same way. The renderer,
/// not the model, draws the headline/price/CTA — so they're always crisp and accurate, which
/// text-in-image generation is unreliable at.</summary>
public sealed record PosterSpec(
    string Kind, string Headline, decimal? Price, string Cta,
    string? CompanyName, bool IncludeName, bool IncludeLogo, string? LogoUrl, string? ProductImageUrl,
    string Primary, string Secondary, string Accent, string? Font, string? BackgroundImageUrl = null,
    string? HeadlineFont = null, string HeadlineScale = "medium", string TemplateId = "bold-medallion",
    string Format = "square");

/// <summary>One selectable poster layout — an id/name/description for the picker plus whether it makes
/// use of a photo (so the UI can hint "add a photo for this one" vs. "this one is photo-optional") and
/// which category it's grouped under in a Canva-style browse grid (<see cref="PrebuiltThemeRegistry"/>'s
/// storefront-theme catalog uses the same category-tag pattern).</summary>
public sealed record PosterTemplateInfo(string Id, string Name, string Description, bool UsesPhoto, string Category);

/// <summary>One selectable canvas size. Both formats currently ship at a fixed 1080 width — matching
/// Instagram/Facebook's own convention of a constant feed width with the height varying by placement —
/// so templates only need to adapt their vertical layout, not re-flow horizontally, per format.</summary>
public sealed record PosterFormatInfo(string Id, string Label, int Width, int Height);

/// <summary>Renders a poster to markup. Today: self-contained SVG (no browser, no infra) — renders as a
/// real image in the admin and downloads cleanly. Rasterisation to PNG for social upload swaps in later
/// behind this same interface (render worker), with no caller change (plan §3.5/§3.6).</summary>
public interface IPosterRenderer
{
    Task<string> RenderSvgAsync(PosterSpec spec, CancellationToken ct = default);
    IReadOnlyList<string> AvailableFonts { get; }

    /// <summary>The template library — hand-authored layouts a merchant picks from, Canva-style,
    /// grown one at a time (mirrors PrebuiltThemeRegistry's storefront-theme catalog pattern).</summary>
    IReadOnlyList<PosterTemplateInfo> AvailableTemplates { get; }

    /// <summary>The canvas sizes a poster can render at — every template must hold up at every one.</summary>
    IReadOnlyList<PosterFormatInfo> AvailableFormats { get; }
}

/// <summary>
/// A small poster TEMPLATE REGISTRY (not one generative algorithm trying to be infinitely flexible) —
/// each template is a hand-composed SVG layout sharing common building blocks (flat colour ground,
/// embedded Google-Font import, wrapped bold headline, logo, price badge, CTA pill). Google Fonts are
/// imported INSIDE the SVG's own &lt;style&gt; (page-level &lt;link&gt; stylesheets don't apply to SVG
/// rendered via &lt;img src&gt; — it's an isolated rendering context), so the chosen font actually
/// renders wherever this SVG is shown. Images that aren't ours (or fail to load) are simply skipped, so
/// a poster always renders.
///
/// Layout math: canvas width is a constant 1080 across every format we ship (Instagram/Facebook keep
/// feed width fixed and vary height by placement), so only Y-axis positions need to adapt per format.
/// Elements anchored to the top (logo, headline start) use fixed offsets — they look identical at the
/// square baseline regardless of extra height below. Elements anchored to the bottom (the CTA pill) are
/// computed from the actual canvas height so they never end up stranded mid-canvas on a tall format. The
/// photo medallion grows modestly into whatever extra height a taller format provides, rather than
/// staying pinned at its square-format size and leaving the story format looking sparse.
/// </summary>
public sealed class SvgPosterRenderer(IMediaStorage media) : IPosterRenderer
{
    // Mirrors theme.service.ts KNOWN_FONTS, narrowed to faces that hold up as a big bold poster
    // headline (a thin/light body face reads weak at 80px+, even requested at weight 900).
    public IReadOnlyList<string> AvailableFonts { get; } =
        new[] { "Poppins", "Montserrat", "Archivo", "Oswald", "Bebas Neue", "Space Grotesk", "DM Sans" };

    public IReadOnlyList<PosterTemplateInfo> AvailableTemplates { get; } = new[]
    {
        new PosterTemplateInfo("bold-medallion", "Bold Medallion",
            "A flat brand-colour ground with your product/scene in a bold circular medallion bleeding off the frame — confident, editorial.",
            UsesPhoto: true, Category: "Product Spotlight"),
        new PosterTemplateInfo("minimal-type", "Minimal Type",
            "A two-tone colour-block poster built entirely from typography — no photo needed. Great for sales, offers and announcements.",
            UsesPhoto: false, Category: "Sale & Offer"),
    };

    public IReadOnlyList<PosterFormatInfo> AvailableFormats { get; } = new[]
    {
        new PosterFormatInfo("square", "Square · Instagram & Facebook feed", 1080, 1080),
        new PosterFormatInfo("story", "Story · Instagram & Facebook stories", 1080, 1920),
    };

    /// <summary>Shared, precomputed pieces every template composes with — the async I/O (font choice,
    /// embedded logo/photo data-URIs) happens once here regardless of which template renders.</summary>
    private sealed record Ctx(string Font, string Primary, string Secondary, string Accent, string Darker, string? Logo, string? Photo, int W, int H);

    public async Task<string> RenderSvgAsync(PosterSpec spec, CancellationToken ct = default)
    {
        var displayFont = string.IsNullOrWhiteSpace(spec.HeadlineFont)
            ? (string.IsNullOrWhiteSpace(spec.Font) ? "Poppins" : spec.Font!) : spec.HeadlineFont!;
        if (!AvailableFonts.Contains(displayFont, StringComparer.OrdinalIgnoreCase)) displayFont = "Poppins";

        var format = AvailableFormats.FirstOrDefault(f => f.Id.Equals(spec.Format, StringComparison.OrdinalIgnoreCase)) ?? AvailableFormats[0];

        var primary = SafeColor(spec.Primary, "#111827");
        var secondary = SafeColor(spec.Secondary, "#374151");
        var accent = SafeColor(spec.Accent, "#2563eb");
        var darker = Darken(primary, 0.35);

        var logo = spec.IncludeLogo ? await DataUriAsync(spec.LogoUrl, ct) : null;
        var background = await DataUriAsync(spec.BackgroundImageUrl, ct);
        var photo = background ?? await DataUriAsync(spec.ProductImageUrl, ct);

        var ctx = new Ctx(displayFont, primary, secondary, accent, darker, logo, photo, format.Width, format.Height);
        var body = (spec.TemplateId ?? "").ToLowerInvariant() switch
        {
            "minimal-type" => ComposeMinimalType(spec, ctx),
            _ => ComposeBoldMedallion(spec, ctx),
        };

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{ctx.W}\" height=\"{ctx.H}\" viewBox=\"0 0 {ctx.W} {ctx.H}\">");
        sb.Append($"<defs><style>@import url('https://fonts.googleapis.com/css2?family={Uri.EscapeDataString(ctx.Font)}:wght@700;800;900&amp;display=swap');</style></defs>");
        sb.Append(body);
        sb.Append("</svg>");
        return sb.ToString();
    }

    // ---- Template: Bold Medallion — flat ground, ambient corner circles, a big circular photo
    //      "stage" bleeding off the bottom-right edge, full-width caps headline across the top. At the
    //      square baseline (H=1080) every number below matches the original hand-tuned layout exactly;
    //      a taller format only changes where the CTA anchors and how large the medallion grows. ----
    private string ComposeBoldMedallion(PosterSpec spec, Ctx c)
    {
        var font = XmlEscape(c.Font) + ", Arial, sans-serif";
        var (headlineSize, lineHeight, maxLines) = SizeFor(spec.HeadlineScale);
        var extra = Math.Max(0, c.H - 1080);   // vertical room beyond the square baseline

        var sb = new StringBuilder();
        sb.Append($"<rect width=\"{c.W}\" height=\"{c.H}\" fill=\"{c.Primary}\"/>");
        sb.Append($"<circle cx=\"-60\" cy=\"{c.H + 40}\" r=\"260\" fill=\"{c.Darker}\" opacity=\"0.5\"/>");
        sb.Append($"<circle cx=\"1040\" cy=\"-40\" r=\"170\" fill=\"{c.Accent}\" opacity=\"0.18\"/>");

        if (c.Photo is not null)
        {
            // Grows into extra height rather than staying pinned at its square-format size, so a story
            // poster doesn't read as a square poster floating in a tall empty frame.
            var r = 400 + extra * 0.15;
            var cy = c.H - 220 - extra * 0.06;
            sb.Append($"<clipPath id=\"medallion\"><circle cx=\"780\" cy=\"{cy:0}\" r=\"{r:0}\"/></clipPath>");
            sb.Append($"<circle cx=\"780\" cy=\"{cy:0}\" r=\"{r:0}\" fill=\"{c.Accent}\"/>");
            sb.Append($"<image href=\"{c.Photo}\" x=\"{780 - r:0}\" y=\"{cy - r:0}\" width=\"{r * 2:0}\" height=\"{r * 2:0}\" clip-path=\"url(#medallion)\" preserveAspectRatio=\"xMidYMid slice\"/>");
        }

        if (c.Logo is not null)
            sb.Append($"<image href=\"{c.Logo}\" x=\"72\" y=\"60\" width=\"220\" height=\"96\" preserveAspectRatio=\"xMinYMid meet\"/>");
        else if (spec.IncludeName && !string.IsNullOrWhiteSpace(spec.CompanyName))
            sb.Append($"<text x=\"72\" y=\"126\" font-family=\"{font}\" font-size=\"38\" font-weight=\"700\" fill=\"#ffffff\">{XmlEscape(spec.CompanyName!)}</text>");

        var lines = Wrap(spec.Headline.ToUpperInvariant(), c.Photo is not null ? 17 : 20, maxLines);
        var startY = 260;
        for (var i = 0; i < lines.Count; i++)
            sb.Append($"<text x=\"72\" y=\"{startY + i * lineHeight}\" font-family=\"{font}\" font-size=\"{headlineSize}\" font-weight=\"900\" fill=\"#ffffff\">{XmlEscape(lines[i])}</text>");
        var afterHeadline = startY + lines.Count * lineHeight + 40;

        if (spec.Price is { } price && price > 0)
        {
            sb.Append($"<rect x=\"72\" y=\"{afterHeadline}\" width=\"250\" height=\"78\" rx=\"12\" fill=\"{c.Accent}\"/>");
            sb.Append($"<text x=\"197\" y=\"{afterHeadline + 52}\" font-family=\"{font}\" font-size=\"42\" font-weight=\"800\" fill=\"#ffffff\" text-anchor=\"middle\">₹{price:0}</text>");
        }

        var ctaTop = c.H - 140;                // baseline (H=1080) gives 940, matching the original fixed value
        var cta = XmlEscape(string.IsNullOrWhiteSpace(spec.Cta) ? "Shop Now" : spec.Cta);
        sb.Append($"<rect x=\"72\" y=\"{ctaTop}\" width=\"300\" height=\"88\" rx=\"44\" fill=\"#ffffff\"/>");
        sb.Append($"<text x=\"222\" y=\"{ctaTop + 56}\" font-family=\"{font}\" font-size=\"38\" font-weight=\"800\" fill=\"{c.Primary}\" text-anchor=\"middle\">{cta}</text>");
        return sb.ToString();
    }

    // ---- Template: Minimal Type — a bold horizontal two-tone colour block, no photo, headline free
    //      to run large across the whole upper zone, price treated as a big standalone number in the
    //      lower accent band rather than a small badge. Reads strong for sales/offers/announcements.
    //      The split between the two colour bands stays at the same 63/37 ratio at every format, so a
    //      taller canvas gets proportionally more of both, not a stretched-out bottom band. ----
    private string ComposeMinimalType(PosterSpec spec, Ctx c)
    {
        var font = XmlEscape(c.Font) + ", Arial, sans-serif";
        var splitY = (int)(c.H * 0.63);   // baseline (H=1080) gives 680, matching the original fixed value

        var sb = new StringBuilder();
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{c.W}\" height=\"{splitY}\" fill=\"{c.Primary}\"/>");
        sb.Append($"<rect x=\"0\" y=\"{splitY}\" width=\"{c.W}\" height=\"{c.H - splitY}\" fill=\"{c.Accent}\"/>");
        sb.Append($"<circle cx=\"1020\" cy=\"60\" r=\"140\" fill=\"{c.Darker}\" opacity=\"0.35\"/>");

        if (c.Logo is not null)
            sb.Append($"<image href=\"{c.Logo}\" x=\"72\" y=\"56\" width=\"220\" height=\"90\" preserveAspectRatio=\"xMinYMid meet\"/>");
        else if (spec.IncludeName && !string.IsNullOrWhiteSpace(spec.CompanyName))
            sb.Append($"<text x=\"72\" y=\"118\" font-family=\"{font}\" font-size=\"36\" font-weight=\"700\" fill=\"#ffffff\">{XmlEscape(spec.CompanyName!)}</text>");

        // Larger canvas for type since there's no photo competing for space.
        var lines = Wrap(spec.Headline.ToUpperInvariant(), 16, 5);
        var lineHeight = lines.Count <= 3 ? 104 : 84;
        var fontSize = lines.Count <= 3 ? 92 : 74;
        var startY = 260;
        for (var i = 0; i < lines.Count; i++)
            sb.Append($"<text x=\"72\" y=\"{startY + i * lineHeight}\" font-family=\"{font}\" font-size=\"{fontSize}\" font-weight=\"900\" fill=\"#ffffff\">{XmlEscape(lines[i])}</text>");

        // Thin rule under the headline as a graphic accent.
        var ruleY = startY + lines.Count * lineHeight - (lines.Count <= 3 ? 60 : 48);
        sb.Append($"<rect x=\"72\" y=\"{Math.Min(ruleY, splitY - 60)}\" width=\"140\" height=\"6\" fill=\"#ffffff\" opacity=\"0.85\"/>");

        // Price as a big standalone figure, vertically centred in the accent band (a design element,
        // not a small badge) — clamped so it never crowds the CTA pill on a tall format.
        var bandCenterY = Math.Min(splitY + (c.H - splitY) / 2, c.H - 220);
        if (spec.Price is { } price && price > 0)
            sb.Append($"<text x=\"72\" y=\"{bandCenterY + 20}\" font-family=\"{font}\" font-size=\"96\" font-weight=\"900\" fill=\"{c.Primary}\">₹{price:0}</text>");

        var cta = XmlEscape(string.IsNullOrWhiteSpace(spec.Cta) ? "Shop Now" : spec.Cta);
        sb.Append($"<rect x=\"72\" y=\"{c.H - 130}\" width=\"300\" height=\"88\" rx=\"44\" fill=\"{c.Primary}\"/>");
        sb.Append($"<text x=\"222\" y=\"{c.H - 76}\" font-family=\"{font}\" font-size=\"38\" font-weight=\"800\" fill=\"#ffffff\" text-anchor=\"middle\">{cta}</text>");
        return sb.ToString();
    }

    private static (int size, int lineHeight, int maxLines) SizeFor(string? scale) => scale?.ToLowerInvariant() switch
    {
        "small" => (64, 74, 4),
        "large" => (96, 106, 3),
        _ => (80, 90, 4),
    };

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
