using System.Text;
using System.Text.Json;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Growth;
using ecomm.api.Features.Media;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>What the merchant controls when crafting one poster by hand — organization or product-led,
/// with full override of what's rendered on the image. <paramref name="BackgroundImageUrl"/> is set
/// once <see cref="IPosterStudioService.GenerateBackgroundAsync"/> has produced an AI scene; omit it to
/// keep the plain brand-gradient look.</summary>
public sealed record PosterStudioRequest(
    string Kind,                 // "org" | "product"
    long? ProductId,
    string Headline,
    decimal? Price,              // null = no price badge, even if the product has one
    string Cta,
    bool IncludeLogo,
    bool IncludeName,
    string? BackgroundImageUrl = null,
    string? TemplateId = null,   // null = the renderer's default template
    string? Font = null,         // null = the brand kit's font
    string? HeadlineScale = null,    // "small" | "medium" | "large"; null = medium
    string? Format = null,           // null = square; see IPosterRenderer.AvailableFormats
    string? PrimaryColor = null,     // null = the brand kit's colour; a per-poster override otherwise
    string? SecondaryColor = null,
    string? AccentColor = null);

public sealed record PosterPreviewResult(string Svg);
public sealed record PosterCreatedResult(long ItemId, long CreativeId, string MediaUrl, string Caption);
public sealed record SuggestHeadlineResult(string Headline);
public sealed record PosterBackgroundStyleDto(string Key, string Label, string Description);
public sealed record PosterBackgroundResult(string Url, int CreditsSpent);
public sealed record PosterEditorOptionsDto(
    IReadOnlyList<PosterTemplateInfo> Templates, IReadOnlyList<string> Fonts,
    IReadOnlyList<PosterBackgroundStyleDto> BackgroundStyles, IReadOnlyList<PosterFormatInfo> Formats);

/// <summary>What the Editor needs to reopen an existing poster. <paramref name="SpecKind"/> is the
/// explicit discriminator: <c>"layers-v1"</c> means <paramref name="Document"/> is populated and the
/// freeform canvas editor can fully reopen it; <c>"legacy"</c> means <paramref name="LegacyPoster"/> is
/// populated (a poster made before the canvas editor existed — still viewable/duplicable, editable only
/// through the old flat-field form); <c>"none"</c> means there's nothing to reopen (a text creative, or
/// a poster with a corrupt/missing spec) and the caller should fall back to a plain view of the image.</summary>
public sealed record PosterDetailDto(
    long CreativeId, long ItemId, string Type, string SpecKind,
    PosterDocument? Document, PosterStudioRequest? LegacyPoster, string? Caption, string? MediaUrl);

/// <summary>A populated starting point for the Editor — pick a template and a headline for the
/// merchant instead of a blank page. See <see cref="IPosterStudioService.AutoFillDraftAsync"/>.</summary>
public sealed record AutoFillDraftResult(string TemplateId, string Headline, bool ShowPrice);

public interface IPosterStudioService
{
    /// <summary>Templates, fonts and background styles the Poster Studio editor offers.</summary>
    PosterEditorOptionsDto Options();

    /// <summary>Renders a poster from the given inputs and returns raw SVG markup — no persistence, no
    /// credit spend. For live-editing: call this on every "Preview"/"Regenerate" click.</summary>
    Task<PosterPreviewResult> PreviewAsync(PosterStudioRequest req, CancellationToken ct = default);

    IReadOnlyList<PosterBackgroundStyleDto> BackgroundStyles();

    /// <summary>Generates a real AI scene (via the same image pipeline "Product images" uses) to sit
    /// behind the poster's text, instead of the plain brand gradient — credit-metered (real provider
    /// cost), opt-in. For a product poster this rides the proven product-image generator directly (same
    /// quality the merchant already sees on Product images); for an organization poster it prompts from
    /// the brand kit. The renderer still draws the headline/price/CTA on top — never the model — since
    /// image models render exact text/prices unreliably.</summary>
    Task<PosterBackgroundResult> GenerateBackgroundAsync(PosterStudioRequest req, string style, long? userId, CancellationToken ct = default);

    /// <summary>AI-drafted headline options for the product/topic — one metered call, opt-in (never
    /// fired automatically while the merchant is just typing).</summary>
    Task<SuggestHeadlineResult> SuggestHeadlineAsync(long? productId, string? topic, CancellationToken ct = default);

    /// <summary>Finalizes the poster the merchant is happy with: stores the image, writes an AI caption
    /// (credit-metered) and creates a standalone MarketingCreative — ready to be scheduled to a channel
    /// via <see cref="IMarketingGenerationService.ScheduleExistingAsync"/>. Lives outside any specific
    /// week's plan (an internal "ad hoc" bucket) so it doesn't interfere with "This week".</summary>
    Task<PosterCreatedResult> CreateAsync(PosterStudioRequest req, long? userId, CancellationToken ct = default);

    /// <summary>Loads an existing poster creative's editable spec so the Editor can reopen it exactly as
    /// it was left — see <see cref="PosterDetailDto.SpecKind"/> for how the caller should branch.</summary>
    Task<PosterDetailDto> GetAsync(long creativeId, CancellationToken ct = default);

    /// <summary>Re-renders and overwrites an existing poster creative in place — free (no credit spend;
    /// only the explicit "Suggest caption"/"Generate background" actions cost credits, never a save).
    /// The caption is a hand-editable field in the Editor, not regenerated on every save.</summary>
    Task<PosterCreatedResult> UpdateAsync(long creativeId, PosterStudioRequest req, string caption, long? userId, CancellationToken ct = default);

    /// <summary>Clones an existing poster creative into a new, independently-editable Library entry —
    /// copies the rendered image/caption/spec as-is (no re-render, no credit spend), the same "fresh row
    /// from the source's content" pattern <c>ThemeLibraryService.DuplicateAsync</c> uses for themes.</summary>
    Task<PosterCreatedResult> DuplicateAsync(long creativeId, CancellationToken ct = default);

    /// <summary>Populates a starting draft instead of a blank editor: picks a template by a simple rule
    /// (a product poster with a photo gets a photo-capable template, everything else gets a typography
    /// one — no LLM call for this part, deliberately the cheap version first) and reuses the existing
    /// headline suggester. Costs whatever <see cref="SuggestHeadlineAsync"/> already costs today — it
    /// never additionally triggers the paid AI background generation, which stays an explicit opt-in.</summary>
    Task<AutoFillDraftResult> AutoFillDraftAsync(string kind, long? productId, CancellationToken ct = default);

    /// <summary>A template's starter layer document at one format, with its <c>{primary}</c>/
    /// <c>{primaryDark}</c>/<c>{secondary}</c>/<c>{accent}</c>/<c>{font}</c> placeholder tokens resolved
    /// against the tenant's brand kit — the freeform canvas editor's equivalent of the old flat-field
    /// editor defaulting colours/font from the brand kit.</summary>
    Task<PosterDocument> TemplateDocumentAsync(string templateId, string format, CancellationToken ct = default);

    /// <summary>Finalizes a freeform poster: validates the layer document, resolves
    /// <paramref name="mediaFileId"/> to the URL of an image the merchant already uploaded (the client
    /// exports the canvas to PNG and uploads it through the existing media endpoint — never trusts a
    /// raw client-supplied URL string), and persists it exactly like <see cref="CreateAsync"/> does,
    /// just without any server-side rendering. <paramref name="caption"/> is optional — when omitted,
    /// this writes one via the AI copywriter (credit-metered), same as the legacy create path, so
    /// "Create" stays the one metered step regardless of which editor produced the poster.</summary>
    Task<PosterCreatedResult> CreateFromDocumentAsync(PosterDocument doc, long mediaFileId, string? caption, long? userId, CancellationToken ct = default);

    /// <summary>The document-based equivalent of <see cref="UpdateAsync"/> — free, no credit spend.</summary>
    Task<PosterCreatedResult> UpdateFromDocumentAsync(long creativeId, PosterDocument doc, long mediaFileId, string caption, long? userId, CancellationToken ct = default);
}

/// <summary>
/// A standalone poster editor (Marketing Studio, per the user's "make it more flexible, like a real
/// editor" ask) — separate from the weekly-plan batch flow. Two-phase by design to protect credits:
/// Preview is free (deterministic SVG render only) so the merchant can iterate on headline/price/CTA/
/// toggles as many times as they like; Create is the one metered step (an AI caption for the eventual
/// post) that persists the result. Reuses the same renderer/brand-kit/catalog the weekly plan uses.
/// </summary>
public sealed class PosterStudioService(
    EcommerceDbContext db, IPosterRenderer renderer, IMarketingBrandService brandService,
    ICatalogReader catalog, IMarketingCopywriter copywriter, IMediaStorage media,
    IGrowthImageService growthImages, IAiCreditService credits, IPosterDocumentValidator documentValidator) : IPosterStudioService
{
    // Sentinel far outside any real computed week-start date — guarantees the ad-hoc bucket can never
    // collide with AutoDraftForCurrentTenantAsync's "does a plan already exist for this week" check.
    private static readonly DateTime AdHocWeekStart = new(1970, 1, 1);

    // camelCase specifically for PosterDocument (de)serialization — PosterSpecReader/the schema/the
    // frontend all agree on "specVersion" lowercase-first. Legacy PosterStudioRequest (de)serialization
    // elsewhere in this class deliberately keeps the default (PascalCase) options unchanged — those are
    // already-persisted rows whose on-disk format must not shift.
    private static readonly JsonSerializerOptions DocJsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Org-level (no product) prompts, keyed to the same style labels the product-image generator uses
    // (reused as-is for products via IGrowthImageService) so the picker reads consistently either way.
    private static readonly Dictionary<string, (string Label, string Description, string Instruction)> OrgStyles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lifestyle"] = ("Lifestyle photo", "A natural, in-use retail scene.",
            "A photorealistic lifestyle retail scene evoking the brand, warm natural light, shallow depth of field, tasteful negative space in the lower third for a text overlay. No text, no watermark, no logos."),
        ["studio"] = ("Studio shot", "Clean, minimal studio backdrop.",
            "A clean, minimal studio backdrop in soft even light, subtle brand-appropriate colour tones, elegant negative space in the lower third for a text overlay. No text, no watermark, no logos."),
        ["festive"] = ("Festive poster", "A celebratory festival-themed scene.",
            "A vibrant festive promotional scene for an Indian festival, warm celebratory colours and decorative elements, clear open space in the lower third for a text overlay. No text, no watermark, no logos."),
        ["flatlay"] = ("Flat lay", "An overhead styled composition.",
            "An overhead flat-lay photograph with tasteful props on a textured surface, balanced composition, bright even light, open space in the lower third for a text overlay. No text, no watermark, no logos."),
    };

    public IReadOnlyList<PosterBackgroundStyleDto> BackgroundStyles() =>
        OrgStyles.Select(kv => new PosterBackgroundStyleDto(kv.Key, kv.Value.Label, kv.Value.Description)).ToList();

    public PosterEditorOptionsDto Options() =>
        new(renderer.AvailableTemplates, renderer.AvailableFonts, BackgroundStyles(), renderer.AvailableFormats);

    public async Task<PosterPreviewResult> PreviewAsync(PosterStudioRequest req, CancellationToken ct = default)
    {
        var spec = await BuildSpecAsync(req, ct);
        return new PosterPreviewResult(await renderer.RenderSvgAsync(spec, ct));
    }

    public async Task<PosterBackgroundResult> GenerateBackgroundAsync(PosterStudioRequest req, string style, long? userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(style) || !OrgStyles.ContainsKey(style))
            throw new AppException("Unknown background style.", StatusCodes.Status400BadRequest);

        if (req.Kind == "product")
        {
            if (req.ProductId is not { } pid)
                throw new AppException("Choose a product for a product poster.", StatusCodes.Status400BadRequest);
            // Ride the same, already-proven product-image pipeline the merchant sees on Product images —
            // same quality, same 20-credit price, no duplicate prompt engineering.
            var image = await growthImages.GenerateAsync(new GenerateImageRequest(pid, style, "instagram-post", req.Headline), userId, ct);
            return new PosterBackgroundResult(image.Url, AiCreditPricing.CostOf(AiCreditPricing.GrowthImage));
        }

        // Organization poster — no product to anchor a prompt on, so build one from the brand kit.
        var brand = await brandService.GetAsync(ct);
        var (_, _, instruction) = OrgStyles[style];
        var subject = string.IsNullOrWhiteSpace(brand.CompanyName) ? "a retail store" : $"the brand \"{brand.CompanyName}\"";
        var prompt = $"A marketing scene for {subject}. {instruction}";

        var url = await credits.MeterImageAsync(AiCreditPricing.GrowthImage, async img =>
        {
            var image = await img.GenerateAsync(new ImagePrompt(prompt, "1024x1024"), ct);
            using var stream = new MemoryStream(image.Bytes);
            var stored = await media.SaveAsync(stream, $"poster-bg-{style}.png", image.ContentType, ct);
            return (stored.Url, image);
        }, ct);

        return new PosterBackgroundResult(url, AiCreditPricing.CostOf(AiCreditPricing.GrowthImage));
    }

    public async Task<SuggestHeadlineResult> SuggestHeadlineAsync(long? productId, string? topic, CancellationToken ct = default)
    {
        var product = productId is { } pid ? await catalog.GetAsync(pid, ct) : null;
        var subject = product?.Name ?? topic ?? "our store";
        var brief = $"Write ONE short, punchy poster headline (max 8 words, no quotes, no hashtags) for {subject}.";
        var text = await copywriter.WriteAsync("instagram-caption", productId, brief, null, ct);
        // Copy generation returns a fuller caption — take the first line/sentence as the headline.
        var headline = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? subject;
        headline = headline.Split('.').First().Trim(' ', '"', '“', '”');
        if (headline.Length > 60) headline = headline[..60];
        return new SuggestHeadlineResult(string.IsNullOrWhiteSpace(headline) ? subject : headline);
    }

    public async Task<PosterCreatedResult> CreateAsync(PosterStudioRequest req, long? userId, CancellationToken ct = default)
    {
        var spec = await BuildSpecAsync(req, ct);
        var svg = await renderer.RenderSvgAsync(spec, ct);

        var planId = await GetOrCreateAdHocPlanAsync(ct);
        var now = DateTime.UtcNow;
        var item = new MarketingPlanItem
        {
            MarketingPlanId = planId,
            Type = "poster",
            Topic = Clean(req.Headline, 300) ?? "Poster",
            ProductId = req.Kind == "product" ? req.ProductId : null,
            Channels = "",
            IncludeLogo = req.IncludeLogo,
            IncludeName = req.IncludeName,
            Status = "proposed",
            ScheduledAt = now,
            CreatedAt = now,
        };
        db.MarketingPlanItems.Add(item);
        await db.SaveChangesAsync(ct);

        var bytes = Encoding.UTF8.GetBytes(svg);
        var stored = await media.SaveAsync(new MemoryStream(bytes), $"poster-{item.MarketingPlanItemId}.svg", "image/svg+xml", ct);

        var kind = req.Kind == "product" ? "product-description" : "instagram-caption";
        var caption = await copywriter.WriteAsync(kind, item.ProductId, req.Headline, userId, ct);

        var creative = new MarketingCreative
        {
            MarketingPlanItemId = item.MarketingPlanItemId,
            Type = "poster",
            Status = "generated",
            Body = caption,
            OutputMediaUrl = stored.Url,
            Spec = JsonSerializer.Serialize(req),
            ProductId = item.ProductId,
            CreatedAt = now,
        };
        db.MarketingCreatives.Add(creative);
        item.Status = "approved";
        item.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return new PosterCreatedResult(item.MarketingPlanItemId, creative.MarketingCreativeId, stored.Url, caption);
    }

    public async Task<PosterDetailDto> GetAsync(long creativeId, CancellationToken ct = default)
    {
        var creative = await db.MarketingCreatives.AsNoTracking().FirstOrDefaultAsync(c => c.MarketingCreativeId == creativeId, ct)
            ?? throw new AppException("Poster not found.", StatusCodes.Status404NotFound);

        if (creative.Type != "poster" || string.IsNullOrWhiteSpace(creative.Spec))
            return new PosterDetailDto(creative.MarketingCreativeId, creative.MarketingPlanItemId, creative.Type,
                "none", null, null, creative.Body, creative.OutputMediaUrl);

        if (PosterSpecReader.IsLayersDocument(creative.Spec))
        {
            var doc = JsonSerializer.Deserialize<PosterDocument>(creative.Spec, DocJsonOpts);   // written by us, after validation — trusted
            return new PosterDetailDto(creative.MarketingCreativeId, creative.MarketingPlanItemId, creative.Type,
                "layers-v1", doc, null, creative.Body, creative.OutputMediaUrl);
        }

        PosterStudioRequest? legacy = null;
        try { legacy = JsonSerializer.Deserialize<PosterStudioRequest>(creative.Spec); }
        catch (JsonException) { /* genuinely corrupt row — SpecKind stays "legacy" with LegacyPoster null */ }
        return new PosterDetailDto(creative.MarketingCreativeId, creative.MarketingPlanItemId, creative.Type,
            "legacy", null, legacy, creative.Body, creative.OutputMediaUrl);
    }

    public async Task<PosterDocument> TemplateDocumentAsync(string templateId, string format, CancellationToken ct = default)
    {
        var doc = PosterTemplateDocumentRegistry.StarterDocument(templateId, format)
            ?? throw new AppException("Unknown template.", StatusCodes.Status400BadRequest);
        var brand = await brandService.GetAsync(ct);
        return ResolveBrandColors(doc, brand);
    }

    public async Task<PosterCreatedResult> CreateFromDocumentAsync(PosterDocument doc, long mediaFileId, string? caption, long? userId, CancellationToken ct = default)
    {
        var errors = documentValidator.Validate(doc);
        if (errors.Count > 0) throw new AppException(string.Join(" ", errors), StatusCodes.Status400BadRequest);

        var mediaFile = await db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.MediaFileId == mediaFileId, ct)
            ?? throw new AppException("Uploaded image not found.", StatusCodes.Status400BadRequest);

        var planId = await GetOrCreateAdHocPlanAsync(ct);
        var now = DateTime.UtcNow;
        var headline = doc.Layers.FirstOrDefault(l => l.Role == "headline")?.Text;

        // "Create" stays the one credit-metered step regardless of editor shape: if the caller hasn't
        // already got a caption (the freeform editor doesn't yet offer its own "suggest caption" control
        // — that lands with the properties panel), write one the same way the legacy CreateAsync always
        // has, rather than silently shipping posters with no caption.
        var resolvedCaption = string.IsNullOrWhiteSpace(caption)
            ? await copywriter.WriteAsync(doc.Kind == "product" ? "product-description" : "instagram-caption", doc.Kind == "product" ? doc.ProductId : null, headline ?? "our store", userId, ct)
            : caption;

        var item = new MarketingPlanItem
        {
            MarketingPlanId = planId,
            Type = "poster",
            Topic = Clean(headline, 300) ?? "Poster",
            ProductId = doc.Kind == "product" ? doc.ProductId : null,
            Channels = "",
            IncludeLogo = doc.Layers.Any(l => l.Role == "logo"),
            IncludeName = false,
            Status = "proposed",
            ScheduledAt = now,
            CreatedAt = now,
        };
        db.MarketingPlanItems.Add(item);
        await db.SaveChangesAsync(ct);

        var creative = new MarketingCreative
        {
            MarketingPlanItemId = item.MarketingPlanItemId,
            Type = "poster",
            Status = "generated",
            Body = Clean(resolvedCaption, 2000),
            OutputMediaUrl = mediaFile.Url,
            Spec = JsonSerializer.Serialize(doc, DocJsonOpts),
            ProductId = item.ProductId,
            CreatedAt = now,
        };
        db.MarketingCreatives.Add(creative);
        item.Status = "approved";
        item.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return new PosterCreatedResult(item.MarketingPlanItemId, creative.MarketingCreativeId, mediaFile.Url, creative.Body ?? "");
    }

    public async Task<PosterCreatedResult> UpdateFromDocumentAsync(long creativeId, PosterDocument doc, long mediaFileId, string caption, long? userId, CancellationToken ct = default)
    {
        var errors = documentValidator.Validate(doc);
        if (errors.Count > 0) throw new AppException(string.Join(" ", errors), StatusCodes.Status400BadRequest);

        var creative = await db.MarketingCreatives.FirstOrDefaultAsync(c => c.MarketingCreativeId == creativeId, ct)
            ?? throw new AppException("Poster not found.", StatusCodes.Status404NotFound);
        if (creative.Type != "poster")
            throw new AppException("Only posters can be edited here.", StatusCodes.Status400BadRequest);

        var mediaFile = await db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.MediaFileId == mediaFileId, ct)
            ?? throw new AppException("Uploaded image not found.", StatusCodes.Status400BadRequest);

        var now = DateTime.UtcNow;
        var headline = doc.Layers.FirstOrDefault(l => l.Role == "headline")?.Text;
        var newProductId = doc.Kind == "product" ? doc.ProductId : null;

        creative.Body = Clean(caption, 2000) ?? creative.Body;
        creative.OutputMediaUrl = mediaFile.Url;
        creative.Spec = JsonSerializer.Serialize(doc, DocJsonOpts);
        creative.ProductId = newProductId;
        creative.UpdatedAt = now;

        var item = await db.MarketingPlanItems.FirstOrDefaultAsync(i => i.MarketingPlanItemId == creative.MarketingPlanItemId, ct);
        if (item is not null)
        {
            item.Topic = Clean(headline, 300) ?? item.Topic;
            item.ProductId = newProductId;
            item.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);

        return new PosterCreatedResult(creative.MarketingPlanItemId, creative.MarketingCreativeId, mediaFile.Url, creative.Body ?? "");
    }

    /// <summary>Substitutes a template's <c>{primary}</c>/<c>{primaryDark}</c>/<c>{secondary}</c>/
    /// <c>{accent}</c>/<c>{onPrimary}</c>/<c>{onAccent}</c>/<c>{font}</c> placeholder tokens with the
    /// tenant's actual brand kit values — the one place a template's pure data becomes tenant-specific,
    /// mirroring how the old flat-field <see cref="BuildSpecAsync"/> defaulted colours/font from the
    /// brand kit. <c>onPrimary</c>/<c>onAccent</c> are computed, not looked up: a template author can't
    /// know in advance whether a given tenant's brand colour will be light or dark, so text meant to sit
    /// on top of a primary/accent-coloured surface should use these rather than a fixed literal colour
    /// — a tenant with a pale brand colour would otherwise get pale-on-pale, unreadable text (exactly
    /// what happened here before this existed).</summary>
    private static PosterDocument ResolveBrandColors(PosterDocument doc, MarketingBrandDto brand)
    {
        var primaryDark = SvgPosterRenderer.Darken(brand.PrimaryColor, 0.35);
        var font = string.IsNullOrWhiteSpace(brand.Font) ? "Poppins" : brand.Font;
        var onPrimary = ReadableTextColor(brand.PrimaryColor);
        var onAccent = ReadableTextColor(brand.AccentColor);

        string? Resolve(string? s) => s switch
        {
            "{primary}" => brand.PrimaryColor,
            "{primaryDark}" => primaryDark,
            "{secondary}" => brand.SecondaryColor,
            "{accent}" => brand.AccentColor,
            "{onPrimary}" => onPrimary,
            "{onAccent}" => onAccent,
            "{font}" => font,
            _ => s,
        };

        var layers = doc.Layers.Select(l => l with
        {
            FontFamily = Resolve(l.FontFamily),
            Color = Resolve(l.Color),
            Fill = Resolve(l.Fill),
            Stroke = Resolve(l.Stroke),
        }).ToList();

        return doc with { Background = doc.Background with { Color = Resolve(doc.Background.Color) }, Layers = layers };
    }

    public async Task<PosterCreatedResult> UpdateAsync(long creativeId, PosterStudioRequest req, string caption, long? userId, CancellationToken ct = default)
    {
        var creative = await db.MarketingCreatives.FirstOrDefaultAsync(c => c.MarketingCreativeId == creativeId, ct)
            ?? throw new AppException("Poster not found.", StatusCodes.Status404NotFound);
        if (creative.Type != "poster")
            throw new AppException("Only posters can be edited here.", StatusCodes.Status400BadRequest);

        var spec = await BuildSpecAsync(req, ct);
        var svg = await renderer.RenderSvgAsync(spec, ct);
        var bytes = Encoding.UTF8.GetBytes(svg);
        var stored = await media.SaveAsync(new MemoryStream(bytes), $"poster-{creative.MarketingPlanItemId}.svg", "image/svg+xml", ct);

        var now = DateTime.UtcNow;
        var newProductId = req.Kind == "product" ? req.ProductId : null;

        creative.Body = Clean(caption, 2000) ?? creative.Body;
        creative.OutputMediaUrl = stored.Url;
        creative.Spec = JsonSerializer.Serialize(req);
        creative.ProductId = newProductId;
        creative.UpdatedAt = now;

        var item = await db.MarketingPlanItems.FirstOrDefaultAsync(i => i.MarketingPlanItemId == creative.MarketingPlanItemId, ct);
        if (item is not null)
        {
            item.Topic = Clean(req.Headline, 300) ?? item.Topic;
            item.ProductId = newProductId;
            item.IncludeLogo = req.IncludeLogo;
            item.IncludeName = req.IncludeName;
            item.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);

        return new PosterCreatedResult(creative.MarketingPlanItemId, creative.MarketingCreativeId, stored.Url, creative.Body ?? "");
    }

    public async Task<PosterCreatedResult> DuplicateAsync(long creativeId, CancellationToken ct = default)
    {
        var src = await db.MarketingCreatives.AsNoTracking().FirstOrDefaultAsync(c => c.MarketingCreativeId == creativeId, ct)
            ?? throw new AppException("Poster not found.", StatusCodes.Status404NotFound);
        if (src.Type != "poster")
            throw new AppException("Only posters can be duplicated here.", StatusCodes.Status400BadRequest);

        var srcItem = await db.MarketingPlanItems.AsNoTracking().FirstOrDefaultAsync(i => i.MarketingPlanItemId == src.MarketingPlanItemId, ct);
        var planId = await GetOrCreateAdHocPlanAsync(ct);
        var now = DateTime.UtcNow;

        var item = new MarketingPlanItem
        {
            MarketingPlanId = planId,
            Type = "poster",
            Topic = (srcItem?.Topic ?? "Poster") + " (copy)",
            ProductId = src.ProductId,
            Channels = "",
            IncludeLogo = srcItem?.IncludeLogo ?? true,
            IncludeName = srcItem?.IncludeName ?? true,
            Status = "proposed",
            ScheduledAt = now,
            CreatedAt = now,
        };
        db.MarketingPlanItems.Add(item);
        await db.SaveChangesAsync(ct);

        var copy = new MarketingCreative
        {
            MarketingPlanItemId = item.MarketingPlanItemId,
            Type = "poster",
            Status = "generated",
            Body = src.Body,
            OutputMediaUrl = src.OutputMediaUrl,
            Spec = src.Spec,
            ProductId = src.ProductId,
            CreatedAt = now,
        };
        db.MarketingCreatives.Add(copy);
        item.Status = "approved";
        item.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return new PosterCreatedResult(item.MarketingPlanItemId, copy.MarketingCreativeId, copy.OutputMediaUrl ?? "", copy.Body ?? "");
    }

    public async Task<AutoFillDraftResult> AutoFillDraftAsync(string kind, long? productId, CancellationToken ct = default)
    {
        CatalogProduct? product = kind == "product" && productId is { } pid ? await catalog.GetAsync(pid, ct) : null;
        var hasPhoto = product?.ImageUrl is not null;

        // Rule-based, not an LLM call — a template already declares whether it wants a photo, so match
        // on that instead of guessing. Falls back to the first template in the registry if nothing
        // matches, so this never breaks as the library grows.
        var template = renderer.AvailableTemplates.FirstOrDefault(t => t.UsesPhoto == hasPhoto)
            ?? renderer.AvailableTemplates.FirstOrDefault();

        var headline = await SuggestHeadlineAsync(kind == "product" ? productId : null, null, ct);

        return new AutoFillDraftResult(template?.Id ?? "bold-medallion", headline.Headline, product?.Price is not null);
    }

    private async Task<PosterSpec> BuildSpecAsync(PosterStudioRequest req, CancellationToken ct)
    {
        if (req.Kind == "product" && req.ProductId is null)
            throw new AppException("Choose a product for a product poster.", StatusCodes.Status400BadRequest);

        var brand = await brandService.GetAsync(ct);
        CatalogProduct? product = req.Kind == "product" && req.ProductId is { } pid ? await catalog.GetAsync(pid, ct) : null;
        var headline = Clean(req.Headline, 120) ?? product?.Name ?? brand.CompanyName ?? "Your store";
        var cta = Clean(req.Cta, 40) ?? "Shop Now";

        return new PosterSpec(
            req.Kind == "product" ? "product" : "org", headline, req.Price, cta,
            brand.CompanyName, req.IncludeName, req.IncludeLogo, brand.LogoUrl, product?.ImageUrl,
            string.IsNullOrWhiteSpace(req.PrimaryColor) ? brand.PrimaryColor : req.PrimaryColor,
            string.IsNullOrWhiteSpace(req.SecondaryColor) ? brand.SecondaryColor : req.SecondaryColor,
            string.IsNullOrWhiteSpace(req.AccentColor) ? brand.AccentColor : req.AccentColor,
            brand.Font, req.BackgroundImageUrl,
            string.IsNullOrWhiteSpace(req.Font) ? null : req.Font,
            string.IsNullOrWhiteSpace(req.HeadlineScale) ? "medium" : req.HeadlineScale,
            string.IsNullOrWhiteSpace(req.TemplateId) ? "bold-medallion" : req.TemplateId,
            string.IsNullOrWhiteSpace(req.Format) ? "square" : req.Format);
    }

    private async Task<long> GetOrCreateAdHocPlanAsync(CancellationToken ct)
    {
        var existing = await db.MarketingPlans.FirstOrDefaultAsync(p => p.Status == "adhoc", ct);
        if (existing is not null) return existing.MarketingPlanId;

        var plan = new MarketingPlan { WeekStart = AdHocWeekStart, Status = "adhoc", CreatedAt = DateTime.UtcNow };
        db.MarketingPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return plan.MarketingPlanId;
    }

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }

    /// <summary>Picks black or white for text sitting on top of <paramref name="backgroundHex"/>, via
    /// the standard WCAG relative-luminance formula — reliably readable regardless of how light or dark
    /// a tenant's own brand colour happens to be, rather than assuming any particular brand colour is
    /// "probably dark enough" for white text or vice versa.</summary>
    private static string ReadableTextColor(string backgroundHex)
    {
        var hex = backgroundHex.Length == 4
            ? $"#{backgroundHex[1]}{backgroundHex[1]}{backgroundHex[2]}{backgroundHex[2]}{backgroundHex[3]}{backgroundHex[3]}"
            : backgroundHex;
        if (hex.Length != 7) return "#111827";   // malformed — fall back to dark text, the safer default

        double Channel(int start)
        {
            var v = Convert.ToInt32(hex.Substring(start, 2), 16) / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        var luminance = 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
        return luminance > 0.5 ? "#111827" : "#ffffff";
    }
}
