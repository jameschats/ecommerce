using System.Text.Json;
using ClosedXML.Excel;
using ecomm.api.Common;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Ai;

// ---- Wire shapes (generated preview; the client holds it and posts it back to seed/export) ----
public sealed record GenerateCatalogRequest(string? PresetKey, string? Prompt, int Categories = 6, int ProductsPerCategory = 6);
public sealed record GenProduct(string Name, string ShortDescription, string Description, decimal Price, string? Tags, string ImageUrl);
public sealed record GenCategory(string Name, string Description, IReadOnlyList<GenCategory>? Subcategories, IReadOnlyList<GenProduct> Products);
public sealed record GeneratedCatalog(string StoreType, IReadOnlyList<GenCategory> Categories);
public sealed record SeedResultDto(int Categories, int Products);
public sealed record CatalogPresetDto(string Key, string Label);
public sealed record CatalogStatusDto(bool Enabled, int SampleProducts, IReadOnlyList<CatalogPresetDto> Presets);

public interface IAiCatalogService
{
    CatalogStatusDto Presets(bool enabled, int sampleCount);
    Task<int> SampleCountAsync(CancellationToken ct = default);
    Task<GeneratedCatalog> GenerateAsync(GenerateCatalogRequest req, CancellationToken ct = default);
    Task<SeedResultDto> SeedAsync(GeneratedCatalog catalog, CancellationToken ct = default);
    Task<int> ClearAsync(CancellationToken ct = default);
    byte[] BuildExcel(GeneratedCatalog catalog);
}

/// <summary>
/// AI-2 sample-catalog generator. <see cref="GenerateAsync"/> asks the AI for a small category/product
/// tree (metered as one <c>sample-catalog</c> action) and returns it as a preview — nothing is written
/// until the merchant confirms (the "never blind-apply" guardrail). <see cref="SeedAsync"/> writes it into
/// the store with <c>AI-</c> SKUs so <see cref="ClearAsync"/> can cleanly undo it (the acme-seed convention).
/// </summary>
public sealed class AiCatalogService(EcommerceDbContext db, IAiCreditService credits) : IAiCatalogService
{
    private const string SkuPrefix = "AI-";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public CatalogStatusDto Presets(bool enabled, int sampleCount) => new(
        enabled, sampleCount, SampleCatalogPresets.All.Select(p => new CatalogPresetDto(p.Key, p.Label)).ToList());

    public Task<int> SampleCountAsync(CancellationToken ct = default) =>
        db.Products.CountAsync(p => p.Sku.StartsWith(SkuPrefix) && !p.IsDeleted, ct);

    public async Task<GeneratedCatalog> GenerateAsync(GenerateCatalogRequest req, CancellationToken ct = default)
    {
        var preset = SampleCatalogPresets.Get(req.PresetKey);
        var brief = preset?.Brief ?? (req.Prompt ?? "").Trim();
        if (string.IsNullOrWhiteSpace(brief))
            throw new AppException("Pick a store type or describe your store.", 400);

        var cats = Math.Clamp(req.Categories, 1, 12);
        var per = Math.Clamp(req.ProductsPerCategory, 1, 12);
        var themeKeys = preset?.ThemeKeys ?? new[] { "bazaar" };
        var storeType = preset?.Label ?? "Custom store";

        const string system =
            "You generate a realistic sample product catalog for an online store serving the Indian market. " +
            "Return STRICT JSON of the form: " +
            "{\"categories\":[{\"name\":string,\"description\":string,\"subcategories\":[{\"name\":string,\"description\":string,\"products\":[PRODUCT,...]}],\"products\":[PRODUCT,...]}]} " +
            "where PRODUCT = {\"name\":string,\"shortDescription\":string,\"description\":string,\"price\":number,\"tags\":string}. " +
            "A category has EITHER a non-empty \"subcategories\" array (products live at the leaf) OR its own \"products\" array — never both. " +
            "Prices are plain INR numbers (no symbol), realistic for each item. Names are concise and realistic. " +
            "shortDescription is one line; description is 1-3 sentences. tags is a short comma-separated list. " +
            "Do NOT use real company or brand names. Output ONLY the JSON.";
        var user = $"Store: {brief}.\nGenerate about {cats} top-level categories and about {per} products per category. " +
                   "A few categories may have 2-3 subcategories.";

        // Budget must scale with the requested size, or a large catalog gets truncated mid-JSON and fails to
        // parse. Roughly ~300 tokens per product plus headroom; capped so a runaway request stays bounded.
        var maxTokens = Math.Min(16_000, 1_500 + cats * per * 300);

        var catalog = await credits.MeterAsync(AiCreditPricing.SampleCatalog, async ai =>
        {
            var c = await ai.CompleteAsync(new AiPrompt(system, user, Json: true, MaxTokens: maxTokens), ct);
            var parsed = Parse(c.Text, storeType, themeKeys);
            return (parsed, c);
        }, ct);

        return catalog;
    }

    public async Task<SeedResultDto> SeedAsync(GeneratedCatalog catalog, CancellationToken ct = default)
    {
        if (catalog.Categories.Count == 0) throw new AppException("Nothing to add.", 400);
        var now = DateTime.UtcNow;

        var catSlugs = new HashSet<string>(await db.Categories.Select(c => c.Slug).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
        var existingCats = await db.Categories.ToListAsync(ct);

        // Phase A — top categories. Reuse an existing top-level category with the same name instead of
        // creating a fresh one every time: previously SeedAsync had no idea a category by that name
        // already existed, so re-running "Generate a catalog" (a different store type, a retry, just
        // experimenting) kept creating brand-new "Electronics"/"Mobile Phones"/etc. alongside whatever
        // an earlier run had already added — ClearAsync only ever removed products, never categories
        // ("the merchant may have added to them"), so they piled up across runs with no way back short
        // of manual cleanup. Products still always get created fresh (see Phase C) — a second AI run
        // naming something "Wireless Earbuds" again is plausibly a genuinely different item the merchant
        // explicitly asked for by clicking Generate, not a duplicate to silently drop; a category is
        // pure taxonomy, where two "Electronics" is never actually wanted.
        // GroupBy+First (not ToDictionary) because pre-existing data can already contain same-name
        // duplicates from before this reuse logic shipped — picking the oldest one is deterministic.
        var byName = existingCats.Where(c => c.ParentCategoryId == null)
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.CategoryId).First(), StringComparer.OrdinalIgnoreCase);
        var order = byName.Count == 0 ? 0 : byName.Values.Max(c => c.DisplayOrder) + 1;
        var tops = new List<(GenCategory Gen, Category Cat)>();
        foreach (var top in catalog.Categories)
        {
            var name = Trim(top.Name, 120);
            if (byName.TryGetValue(name, out var reused)) { tops.Add((top, reused)); continue; }

            var cat = new Category
            {
                Name = name, Slug = Unique(Slug.From(name), catSlugs),
                Description = Clip(top.Description, 500), ImageUrl = FirstImage(top),
                DisplayOrder = order++, IsActive = true, CreatedAt = now,
            };
            db.Categories.Add(cat);
            tops.Add((top, cat));
            byName[name] = cat;   // guards a single generated catalog listing the same top name twice
        }
        await db.SaveChangesAsync(ct);   // new tops get ids

        // Phase B — subcategories; collect the leaf categories that will hold products. Same reuse-by-
        // name rule, scoped to whichever parent (new or reused) each subcategory belongs under.
        var leaves = new List<(GenCategory Gen, Category Cat, bool Featured)>();
        foreach (var (gen, cat) in tops)
        {
            if (gen.Subcategories is { Count: > 0 } subs)
            {
                var byNameUnderParent = existingCats.Where(c => c.ParentCategoryId == cat.CategoryId)
                    .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.OrderBy(c => c.CategoryId).First(), StringComparer.OrdinalIgnoreCase);
                var subOrder = byNameUnderParent.Count == 0 ? 0 : byNameUnderParent.Values.Max(c => c.DisplayOrder) + 1;
                var first = true;
                foreach (var sub in subs)
                {
                    var subName = Trim(sub.Name, 120);
                    if (byNameUnderParent.TryGetValue(subName, out var reusedSub))
                    {
                        leaves.Add((sub, reusedSub, first));
                        first = false;
                        continue;
                    }

                    var sc = new Category
                    {
                        Name = subName, Slug = Unique(Slug.From(subName), catSlugs),
                        Description = Clip(sub.Description, 500), ImageUrl = FirstImage(sub),
                        ParentCategoryId = cat.CategoryId,
                        DisplayOrder = subOrder++, IsActive = true, CreatedAt = now,
                    };
                    db.Categories.Add(sc);
                    leaves.Add((sub, sc, first));   // sc.CategoryId is filled after the save below
                    byNameUnderParent[subName] = sc;
                    first = false;
                }
            }
            else leaves.Add((gen, cat, true));
        }
        await db.SaveChangesAsync(ct);   // new subs get ids

        // Phase C — products (+ one image, + starting stock) under each leaf.
        var skus = new HashSet<string>(await db.Products.Select(p => p.Sku).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
        var slugs = new HashSet<string>(await db.Products.Select(p => p.Slug).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
        var prodCount = 0;
        foreach (var (gen, cat, featured) in leaves)
        {
            var firstInCat = true;
            foreach (var gp in gen.Products)
            {
                var name = Trim(gp.Name, 200);
                var product = new Product
                {
                    CategoryId = cat.CategoryId,
                    Sku = Unique($"{SkuPrefix}{Abbrev(cat.Slug)}-{prodCount + 1}", skus),
                    Name = name, Slug = Unique(Slug.From(name), slugs),
                    ShortDescription = Clip(gp.ShortDescription, 300), Description = Clip(gp.Description, 2000),
                    Tags = string.IsNullOrWhiteSpace(gp.Tags) ? null : Trim(gp.Tags, 300),
                    Price = Math.Max(0, gp.Price), Status = "Active", IsActive = true,
                    IsFeatured = featured && firstInCat, CreatedAt = now,
                    Images = new List<ProductImage>
                    {
                        new() { Url = gp.ImageUrl, AltText = name, DisplayOrder = 0, IsPrimary = true, CreatedAt = now },
                    },
                    InventoryRecords = new List<Data.Entities.Inventory>
                    {
                        new() { AvailableQty = 25, ReorderLevel = 5, CreatedAt = now },
                    },
                };
                db.Products.Add(product);
                prodCount++;
                firstInCat = false;
            }
        }
        await db.SaveChangesAsync(ct);

        return new SeedResultDto(catalog.Categories.Count, prodCount);
    }

    public async Task<int> ClearAsync(CancellationToken ct = default)
    {
        // Remove only AI-seeded products (+ their images/stock), mirroring the acme SKU-prefix convention.
        // Categories are left in place (harmless, and the merchant may have added to them).
        var products = await db.Products
            .Include(p => p.Images).Include(p => p.InventoryRecords)
            .Where(p => p.Sku.StartsWith(SkuPrefix))
            .ToListAsync(ct);
        if (products.Count == 0) return 0;

        db.ProductImages.RemoveRange(products.SelectMany(p => p.Images));
        db.RemoveRange(products.SelectMany(p => p.InventoryRecords));
        db.Products.RemoveRange(products);
        await db.SaveChangesAsync(ct);
        return products.Count;
    }

    public byte[] BuildExcel(GeneratedCatalog catalog)
    {
        var headers = new[] { "SKU", "Name", "Category", "Brand", "Price", "CompareAtPrice", "CostPrice", "HsnCode", "Status", "ShortDescription", "ImageUrl", "Description" };
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Products");
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

        var row = 2;
        var n = 1;
        foreach (var (catName, gp) in Flatten(catalog))
        {
            ws.Cell(row, 1).Value = $"{SkuPrefix}{n++}";
            ws.Cell(row, 2).Value = gp.Name;
            ws.Cell(row, 3).Value = catName;
            ws.Cell(row, 5).Value = (double)gp.Price;
            ws.Cell(row, 9).Value = "Active";
            ws.Cell(row, 10).Value = gp.ShortDescription;
            ws.Cell(row, 11).Value = gp.ImageUrl;
            ws.Cell(row, 12).Value = gp.Description;
            row++;
        }
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ---- Parsing + image assignment ----
    private sealed record RawProduct(string? name, string? shortDescription, string? description, decimal? price, string? tags);
    private sealed record RawCategory(string? name, string? description, List<RawCategory>? subcategories, List<RawProduct>? products);
    private sealed record RawCatalog(List<RawCategory>? categories);

    private static GeneratedCatalog Parse(string json, string storeType, IReadOnlyList<string> themeKeys)
    {
        RawCatalog? raw;
        try { raw = JsonSerializer.Deserialize<RawCatalog>(json, JsonOpts); }
        catch (JsonException) { throw new AppException("The AI returned an unexpected response. Please try again.", 502); }

        var cats = raw?.categories;
        if (cats is null || cats.Count == 0)
            throw new AppException("The AI didn't return any products. Please try again.", 502);

        var pool = SampleCatalogImages.For(themeKeys);
        var img = 0;   // running index → images vary across the whole catalog
        string NextImage() => pool[img++ % pool.Count];

        GenProduct MapProduct(RawProduct p) => new(
            (p.name ?? "").Trim(), (p.shortDescription ?? "").Trim(), (p.description ?? "").Trim(),
            p.price is > 0 ? p.price.Value : 0m, string.IsNullOrWhiteSpace(p.tags) ? null : p.tags!.Trim(), NextImage());

        GenCategory? MapCategory(RawCategory c)
        {
            var name = (c.name ?? "").Trim();
            if (name.Length == 0) return null;
            var subs = (c.subcategories ?? new List<RawCategory>())
                .Select(MapCategory).Where(x => x is not null).Cast<GenCategory>().ToList();
            var products = (c.products ?? new List<RawProduct>())
                .Select(MapProduct).Where(p => p.Name.Length > 0).ToList();
            if (subs.Count == 0 && products.Count == 0) return null;
            return new GenCategory(name, (c.description ?? "").Trim(), subs.Count > 0 ? subs : null, products);
        }

        var mapped = cats.Select(MapCategory).Where(x => x is not null).Cast<GenCategory>().ToList();
        if (mapped.Count == 0) throw new AppException("The AI didn't return any usable products. Please try again.", 502);
        return new GeneratedCatalog(storeType, mapped);
    }

    /// <summary>A representative photo for a category — its first product's image, else a descendant's.</summary>
    private static string? FirstImage(GenCategory c)
    {
        var direct = c.Products.FirstOrDefault()?.ImageUrl;
        if (!string.IsNullOrWhiteSpace(direct)) return direct;
        foreach (var s in c.Subcategories ?? Enumerable.Empty<GenCategory>())
        {
            var img = s.Products.FirstOrDefault()?.ImageUrl;
            if (!string.IsNullOrWhiteSpace(img)) return img;
        }
        return null;
    }

    private static IEnumerable<(string CatName, GenProduct Product)> Flatten(GeneratedCatalog catalog)
    {
        foreach (var top in catalog.Categories)
        {
            foreach (var p in top.Products) yield return (top.Name, p);
            if (top.Subcategories is null) continue;
            foreach (var sub in top.Subcategories)
                foreach (var p in sub.Products) yield return (sub.Name, p);
        }
    }

    // ---- small helpers ----
    private static string Unique(string baseVal, HashSet<string> used)
    {
        var v = baseVal;
        var n = 2;
        while (!used.Add(v)) v = $"{baseVal}-{n++}";
        return v;
    }

    private static string Abbrev(string slug)
    {
        var letters = new string(slug.Where(char.IsLetterOrDigit).Take(6).ToArray()).ToUpperInvariant();
        return letters.Length > 0 ? letters : "CAT";
    }

    private static string Trim(string s, int max) { s = (s ?? "").Trim(); return s.Length <= max ? s : s[..max]; }
    private static string? Clip(string? s, int max) { s = (s ?? "").Trim(); return s.Length == 0 ? null : (s.Length <= max ? s : s[..max]); }
}
