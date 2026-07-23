using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Media;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Growth;

public sealed record ImageStyleDto(string Key, string Label, string Description);
public sealed record GenerateImageRequest(long ProductId, string Style, string? Brief);
public sealed record GeneratedImageDto(long Id, string Url, decimal CostInr, DateTime CreatedAt);

public interface IGrowthImageService
{
    IReadOnlyList<ImageStyleDto> Styles();
    Task<GeneratedImageDto> GenerateAsync(GenerateImageRequest req, long? userId, CancellationToken ct = default);
    Task<IReadOnlyList<GeneratedImageDto>> RecentAsync(CancellationToken ct = default);
}

/// <summary>
/// Product marketing images (POC). An honest note on scope: a generative model invents a NEW image from
/// a text description of the product — it does not edit the merchant's actual photo. So this produces
/// lifestyle/marketing imagery "in the spirit of" the product, not a retouch of their real one. Each call
/// costs real money (~₹4-7), so it's metered at a high credit cost and the true rupee spend is recorded.
/// </summary>
public sealed class GrowthImageService(
    EcommerceDbContext db, IAiCreditService credits, IMediaStorage media) : IGrowthImageService
{
    private sealed record StyleDef(string Key, string Label, string Description, string Size, string Instruction);

    private static readonly IReadOnlyList<StyleDef> Defs = new List<StyleDef>
    {
        new("lifestyle", "Lifestyle photo", "The product in a natural, in-use setting.", "1024x1024",
            "A photorealistic lifestyle product photograph, the product shown naturally in an appealing real-world setting, soft natural light, shallow depth of field. No text, no watermark."),
        new("studio", "Studio shot", "Clean studio product shot on a plain background.", "1024x1024",
            "A clean studio product photograph on a smooth seamless background, soft even lighting, crisp focus, e-commerce catalogue style. No text, no watermark."),
        new("festive", "Festive poster", "A celebratory festival-themed promo scene.", "1024x1792",
            "A vibrant festive promotional scene for an Indian festival, warm celebratory colours, decorative elements, the product as the hero. Leave clear space at the top for a headline. No text, no watermark."),
        new("flatlay", "Flat lay", "An overhead styled flat-lay composition.", "1024x1024",
            "An overhead flat-lay photograph, the product styled with complementary props on a textured surface, balanced composition, bright even light. No text, no watermark."),
    };

    private static readonly Dictionary<string, StyleDef> ByKey = Defs.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ImageStyleDto> Styles() =>
        Defs.Select(d => new ImageStyleDto(d.Key, d.Label, d.Description)).ToList();

    public async Task<GeneratedImageDto> GenerateAsync(GenerateImageRequest req, long? userId, CancellationToken ct = default)
    {
        if (!ByKey.TryGetValue(req.Style ?? "", out var style))
            throw new AppException("Unknown image style.", StatusCodes.Status400BadRequest);

        var product = await db.Products.AsNoTracking()
            .Where(p => p.ProductId == req.ProductId)
            .Select(p => new { p.Name, Category = p.Category!.Name, p.ShortDescription })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException("Product not found.", StatusCodes.Status404NotFound);

        var subject =
            $"Product: {product.Name}" +
            (string.IsNullOrWhiteSpace(product.Category) ? "" : $", a {product.Category}") +
            (string.IsNullOrWhiteSpace(product.ShortDescription) ? "" : $". {product.ShortDescription}") +
            (string.IsNullOrWhiteSpace(req.Brief) ? "" : $". {req.Brief!.Trim()}");
        var prompt = $"{subject}. {style.Instruction}";

        var (bytes, contentType, costMicros, url) = await credits.MeterImageAsync(AiCreditPricing.GrowthImage, async img =>
        {
            var image = await img.GenerateAsync(new ImagePrompt(prompt, style.Size), ct);
            using var stream = new MemoryStream(image.Bytes);
            var stored = await media.SaveAsync(stream, $"ai-{style.Key}-{req.ProductId}.png", image.ContentType, ct);
            return ((image.Bytes, image.ContentType, image.CostMicros, stored.Url), image);
        }, ct);

        var record = new GrowthContent
        {
            ContentType = "product-image",
            ProductId = req.ProductId,
            Language = "n/a",
            Title = style.Label,
            Body = url,                      // for an image, the body is the stored URL
            Status = "Draft",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
        };
        db.GrowthContents.Add(record);
        await db.SaveChangesAsync(ct);

        return new GeneratedImageDto(record.GrowthContentId, url, costMicros / 1_000_000m, record.CreatedAt);
    }

    public async Task<IReadOnlyList<GeneratedImageDto>> RecentAsync(CancellationToken ct = default) =>
        await db.GrowthContents.AsNoTracking()
            .Where(c => c.ContentType == "product-image")
            .OrderByDescending(c => c.GrowthContentId).Take(24)
            .Select(c => new GeneratedImageDto(c.GrowthContentId, c.Body, 0m, c.CreatedAt))
            .ToListAsync(ct);
}
