using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Media;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Growth;

public sealed record CatalogImageBackfillItem(string Kind, long Id, string Name, string ImageUrl);
public sealed record CatalogImageBackfillResult(
    int CategoriesEligible, int ProductsEligible,
    IReadOnlyList<CatalogImageBackfillItem> Generated, int Remaining, string? StoppedReason);

public interface ICatalogImageService
{
    Task<CatalogImageBackfillResult> BackfillAsync(int maxItems, CancellationToken ct = default);
}

/// <summary>
/// Real, product/category-specific catalog images (studio-shot style) applied directly as the item's
/// actual image — distinct from <see cref="GrowthImageService"/>'s marketing-image generator, which
/// produces lifestyle/festive drafts a merchant reviews before use. An item is eligible when its current
/// image is missing OR is one of the generic shared sample-catalog placeholders (recognized by the
/// images.unsplash.com host, the pool <c>SampleCatalogImages</c> assigns during AI catalog generation) —
/// a real merchant-uploaded photo is never touched. Capped per call (image generation is slow — several
/// seconds each — and this runs inline in the request) and safe to re-invoke: anything already given a
/// real generated image naturally stops matching the eligibility check.
/// </summary>
public sealed class CatalogImageService(EcommerceDbContext db, IAiCreditService credits, IMediaStorage media) : ICatalogImageService
{
    private const string StudioInstruction =
        "A clean studio product photograph on a smooth seamless background, soft even lighting, crisp focus, e-commerce catalogue style. No text, no watermark.";
    private const string PlaceholderMarker = "images.unsplash.com";

    public async Task<CatalogImageBackfillResult> BackfillAsync(int maxItems, CancellationToken ct = default)
    {
        maxItems = Math.Clamp(maxItems, 1, 50);

        var categories = await db.Categories.Where(c => c.IsActive)
            .Select(c => new { c.CategoryId, c.Name, c.Description, c.ImageUrl }).ToListAsync(ct);
        var eligibleCategories = categories.Where(c => NeedsImage(c.ImageUrl)).ToList();

        var products = await db.Products
            .Select(p => new { p.ProductId, p.Name, p.ShortDescription, Category = p.Category!.Name, Images = p.Images.Select(i => i.Url).ToList() })
            .ToListAsync(ct);
        var eligibleProducts = products.Where(p => p.Images.Count == 0 || p.Images.All(NeedsImage)).ToList();

        var generated = new List<CatalogImageBackfillItem>();
        string? stopped = null;

        // Categories first — far fewer of them, and every storefront page (mega-menu, PLP tiles) shows them.
        foreach (var c in eligibleCategories)
        {
            if (generated.Count >= maxItems) { stopped = "Reached this run's item limit."; break; }
            var prompt = $"Product category: {c.Name}." +
                (string.IsNullOrWhiteSpace(c.Description) ? "" : $" {c.Description}") + $" {StudioInstruction}";

            string url;
            try { url = await GenerateAsync(prompt, ct); }
            catch (AppException ex) when (ex.StatusCode == StatusCodes.Status402PaymentRequired) { stopped = "Out of AI credits."; break; }

            var entity = await db.Categories.FirstAsync(x => x.CategoryId == c.CategoryId, ct);
            entity.ImageUrl = url;
            entity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            generated.Add(new CatalogImageBackfillItem("category", c.CategoryId, c.Name, url));
        }

        if (stopped is null)
        {
            foreach (var p in eligibleProducts)
            {
                if (generated.Count >= maxItems) { stopped = "Reached this run's item limit."; break; }
                var subject = $"Product: {p.Name}" +
                    (string.IsNullOrWhiteSpace(p.Category) ? "" : $", a {p.Category}") +
                    (string.IsNullOrWhiteSpace(p.ShortDescription) ? "" : $". {p.ShortDescription}");
                var prompt = $"{subject}. {StudioInstruction}";

                string url;
                try { url = await GenerateAsync(prompt, ct); }
                catch (AppException ex) when (ex.StatusCode == StatusCodes.Status402PaymentRequired) { stopped = "Out of AI credits."; break; }

                // Replace the placeholder set (if any) with the one real generated image.
                db.ProductImages.RemoveRange(db.ProductImages.Where(i => i.ProductId == p.ProductId));
                db.ProductImages.Add(new ProductImage
                {
                    ProductId = p.ProductId, Url = url, IsPrimary = true, DisplayOrder = 0, CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync(ct);
                generated.Add(new CatalogImageBackfillItem("product", p.ProductId, p.Name, url));
            }
        }

        var totalEligible = eligibleCategories.Count + eligibleProducts.Count;
        return new CatalogImageBackfillResult(eligibleCategories.Count, eligibleProducts.Count, generated, totalEligible - generated.Count, stopped);
    }

    private static bool NeedsImage(string? url) =>
        string.IsNullOrWhiteSpace(url) || url.Contains(PlaceholderMarker, StringComparison.OrdinalIgnoreCase);

    private async Task<string> GenerateAsync(string prompt, CancellationToken ct)
    {
        return await credits.MeterImageAsync(AiCreditPricing.GrowthImage, async img =>
        {
            var image = await img.GenerateAsync(new ImagePrompt(prompt, "1024x1024"), ct);
            using var stream = new MemoryStream(image.Bytes);
            var stored = await media.SaveAsync(stream, "catalog-image.png", image.ContentType, ct);
            return (stored.Url, image);
        }, ct);
    }
}
