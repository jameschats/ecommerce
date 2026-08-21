using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Growth;

public sealed record GrowthTypeDto(string Key, string Label, string Description, int Credits, bool NeedsProduct);
public sealed record GenerateRequest(string ContentType, long? ProductId, string? Language, string? Brief);
public sealed record GrowthContentDto(
    long Id, string ContentType, long? ProductId, long? CampaignId, string Language, string? Title, string Body,
    string Status, bool WasEdited, string? OriginalTitle, string? OriginalBody, DateTime CreatedAt, DateTime? UpdatedAt);

public interface IGrowthGenerationService
{
    IReadOnlyList<GrowthTypeDto> Types();
    Task<GrowthContentDto> GenerateAsync(GenerateRequest req, long? userId, long? campaignId = null, CancellationToken ct = default);
    Task<PagedResult<GrowthContentDto>> LibraryAsync(string? contentType, long? productId, long? campaignId,
        DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);
    Task<GrowthContentDto> UpdateAsync(long id, string body, string? title, string status, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Turns a product (or a free brief) into marketing copy (G1). Each content type is a small data
/// record — key, credit cost, prompt shape — so adding one is a single entry. Every call runs through
/// <see cref="IAiCreditService.MeterAsync"/> (debit on success only) and is saved to the library, so
/// a merchant never pays twice to see something they already generated.
/// </summary>
public sealed class GrowthGenerationService(
    EcommerceDbContext db, IAiCreditService credits, IBrandKitService brandKit) : IGrowthGenerationService
{
    private sealed record TypeDef(
        string Key, string Label, string Description, string Feature, bool NeedsProduct, int MaxTokens,
        bool HasSubject, Func<string, string> Instruction);

    // The order here is the order the UI shows them.
    private static readonly IReadOnlyList<TypeDef> Defs = new List<TypeDef>
    {
        new("instagram-caption", "Instagram caption", "A scroll-stopping caption with hashtags.",
            AiCreditPricing.GrowthInstagram, true, 300, false,
            _ => "Write an Instagram caption: a strong first line, 2-3 short lines of benefit, a clear call to action, then 5-10 relevant hashtags on their own line."),
        new("facebook-post", "Facebook post", "A friendly post for your page.",
            AiCreditPricing.GrowthFacebook, true, 300, false,
            _ => "Write a Facebook post: warm and conversational, 2-4 sentences, one call to action. A few hashtags are optional."),
        new("whatsapp", "WhatsApp broadcast", "A short message for your customer list.",
            AiCreditPricing.GrowthWhatsapp, true, 200, false,
            _ => "Write a WhatsApp broadcast: very short (2-3 lines), personal, with the offer and a call to action. WhatsApp formatting only (*bold*)."),
        new("email", "Email campaign", "A subject line and body for a promo email.",
            AiCreditPricing.GrowthEmail, true, 600, true,
            _ => "Write a promotional email. Return the subject line on the first line prefixed exactly with 'SUBJECT: ', then a blank line, then the body (greeting, 2-3 short paragraphs, a clear call to action)."),
        new("product-description", "Product description", "Persuasive copy for the product page.",
            AiCreditPricing.GrowthProductDescription, true, 400, false,
            _ => "Write a persuasive product description for the store's own product page: 2-3 short paragraphs, benefit-led, factual. No hashtags, no emoji-spam."),
        new("festival-offer", "Festival offer", "A festive promo tied to an occasion.",
            AiCreditPricing.GrowthFestival, false, 300, false,
            brief => $"Write a short festive promotional message for this occasion/offer: \"{brief}\". Warm, celebratory, with a clear call to action."),
        new("google-ads", "Google Ads copy", "Headlines and descriptions for a search ad.",
            AiCreditPricing.GrowthGoogleAds, true, 300, false,
            _ => "Write Google Ads copy: 3 headlines (max 30 characters each) and 2 descriptions (max 90 characters each), each on its own labelled line. Stay within the character limits."),
    };

    private static readonly Dictionary<string, TypeDef> ByKey =
        Defs.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<GrowthTypeDto> Types() =>
        Defs.Select(d => new GrowthTypeDto(d.Key, d.Label, d.Description, AiCreditPricing.CostOf(d.Feature), d.NeedsProduct)).ToList();

    public async Task<GrowthContentDto> GenerateAsync(GenerateRequest req, long? userId, long? campaignId = null, CancellationToken ct = default)
    {
        if (!ByKey.TryGetValue(req.ContentType ?? "", out var def))
            throw new AppException("Unknown content type.", StatusCodes.Status400BadRequest);

        var (brandFragment, kit) = await brandKit.PromptFragmentAsync(ct);
        var language = string.IsNullOrWhiteSpace(req.Language) ? kit.Language : req.Language!.Trim();

        // Product context, only when the type uses it and only for a product this tenant owns.
        string productContext = "";
        if (def.NeedsProduct)
        {
            if (req.ProductId is not { } pid)
                throw new AppException("Pick a product for this content type.", StatusCodes.Status400BadRequest);

            var p = await db.Products.AsNoTracking()
                .Where(x => x.ProductId == pid)
                .Select(x => new { x.Name, x.Price, x.ShortDescription, x.Description, Category = x.Category!.Name })
                .FirstOrDefaultAsync(ct)
                ?? throw new AppException("Product not found.", StatusCodes.Status404NotFound);

            productContext =
                $"PRODUCT\n=======\nName: {p.Name}\nPrice: ₹{p.Price:0}\n" +
                (string.IsNullOrWhiteSpace(p.Category) ? "" : $"Category: {p.Category}\n") +
                (string.IsNullOrWhiteSpace(p.ShortDescription) ? "" : $"Summary: {p.ShortDescription}\n") +
                (string.IsNullOrWhiteSpace(p.Description) ? "" : $"Details: {Trim(p.Description, 800)}\n");
        }

        var brief = Trim(req.Brief, 500);
        var system =
            "You write marketing copy for a small Indian online store. Be specific and factual — use only the " +
            "product facts you're given, never invent specifications, prices, discounts or claims. Output only the " +
            "copy the merchant will use: no preamble, no explanation, no options.\n\n" + brandFragment;

        var user =
            (productContext.Length > 0 ? productContext + "\n" : "") +
            (string.IsNullOrWhiteSpace(brief) ? "" : $"Extra instructions from the merchant: {brief}\n\n") +
            def.Instruction(brief ?? "") +
            $"\n\nWrite it in {language}.";

        var text = await credits.MeterAsync(def.Feature, async ai =>
        {
            var c = await ai.CompleteAsync(new AiPrompt(system, user, Json: false, MaxTokens: def.MaxTokens), ct);
            return (c.Text.Trim(), c);
        }, ct);

        var (title, body) = def.HasSubject ? SplitSubject(text) : (null, text);

        var content = new GrowthContent
        {
            ContentType = def.Key,
            ProductId = def.NeedsProduct ? req.ProductId : null,
            CampaignId = campaignId,
            Language = language,
            Title = title,
            Body = body,
            OriginalTitle = title,
            OriginalBody = body,
            Status = "Draft",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
        };
        db.GrowthContents.Add(content);
        await db.SaveChangesAsync(ct);

        return Map(content);
    }

    public async Task<PagedResult<GrowthContentDto>> LibraryAsync(
        string? contentType, long? productId, long? campaignId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = db.GrowthContents.AsNoTracking().Where(c => c.Status != "Discarded");
        if (!string.IsNullOrWhiteSpace(contentType)) q = q.Where(c => c.ContentType == contentType);
        if (productId is { } pid) q = q.Where(c => c.ProductId == pid);
        if (campaignId is { } cid) q = q.Where(c => c.CampaignId == cid);
        if (from is { } f) q = q.Where(c => c.CreatedAt >= f);
        if (to is { } t) q = q.Where(c => c.CreatedAt < t.AddDays(1));   // inclusive of the whole "to" day

        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(c => c.GrowthContentId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => Map(c)).ToListAsync(ct);

        return new PagedResult<GrowthContentDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<GrowthContentDto> UpdateAsync(long id, string body, string? title, string status, CancellationToken ct = default)
    {
        if (status is not ("Draft" or "Kept" or "Discarded"))
            throw new AppException("Unknown status.", StatusCodes.Status400BadRequest);

        var content = await db.GrowthContents.FirstOrDefaultAsync(c => c.GrowthContentId == id, ct)
                      ?? throw new AppException("Content not found.", StatusCodes.Status404NotFound);

        var trimmed = (body ?? "").Trim();
        if (trimmed.Length == 0) throw new AppException("Content can't be empty.", StatusCodes.Status400BadRequest);

        content.Body = trimmed;
        content.Title = Clean(title, 200);
        content.Status = status;
        content.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(content);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var content = await db.GrowthContents.FirstOrDefaultAsync(c => c.GrowthContentId == id, ct)
                      ?? throw new AppException("Content not found.", StatusCodes.Status404NotFound);
        db.GrowthContents.Remove(content);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Email prompts return "SUBJECT: ...\n\n body". Fall back to no subject if the model didn't comply.</summary>
    private static (string? subject, string body) SplitSubject(string text)
    {
        var lines = text.Split('\n', 2);
        if (lines.Length == 2 && lines[0].TrimStart().StartsWith("SUBJECT:", StringComparison.OrdinalIgnoreCase))
        {
            var subject = lines[0].TrimStart()["SUBJECT:".Length..].Trim();
            return (subject.Length > 0 ? subject : null, lines[1].Trim());
        }
        return (null, text);
    }

    /// <summary>Edited = the current text differs from the generation-time snapshot. Rows from
    /// before OriginalBody existed have it as null — treated as "unknown," never flagged as edited.</summary>
    private static bool WasEdited(GrowthContent c) =>
        c.OriginalBody is not null && (c.Body != c.OriginalBody || c.Title != c.OriginalTitle);

    private static GrowthContentDto Map(GrowthContent c) =>
        new(c.GrowthContentId, c.ContentType, c.ProductId, c.CampaignId, c.Language, c.Title, c.Body,
            c.Status, WasEdited(c), c.OriginalTitle, c.OriginalBody, c.CreatedAt, c.UpdatedAt);

    private static string Trim(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return "";
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
