using System.Linq.Expressions;
using System.Text.Json;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Collections;

/// <summary>One automated-membership rule, e.g. {field:"tag", op:"eq", value:"sale"}.</summary>
public sealed record CollectionRule(string Field, string Op, string Value);

public sealed record CollectionProductDto(long ProductId, string Name, string Slug, decimal Price, string? PrimaryImageUrl);

public sealed record CollectionDto(
    long CollectionId, string Name, string Slug, string? Description, string? ImageUrl,
    string CollectionType, string MatchType, IReadOnlyList<CollectionRule> Rules,
    string? MetaTitle, string? MetaDescription, bool IsActive, int ProductCount);

public sealed record SaveCollectionRequest(
    string Name, string? Slug, string? Description, string? ImageUrl, string CollectionType, string MatchType,
    IReadOnlyList<CollectionRule>? Rules, string? MetaTitle, string? MetaDescription, bool IsActive);

public sealed record PublicCollectionDto(
    long CollectionId, string Name, string Slug, string? Description, string? ImageUrl,
    string? MetaTitle, string? MetaDescription, IReadOnlyList<CollectionProductDto> Products);

public interface ICollectionService
{
    Task<IReadOnlyList<CollectionDto>> ListAsync(CancellationToken ct = default);
    Task<CollectionDto> GetAsync(long id, CancellationToken ct = default);
    Task<CollectionDto> CreateAsync(SaveCollectionRequest req, CancellationToken ct = default);
    Task<CollectionDto> UpdateAsync(long id, SaveCollectionRequest req, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionProductDto>> MembersAsync(long id, bool activeOnly, CancellationToken ct = default);
    /// <summary>Active members projected to the same rich shape the storefront's product grids use
    /// (swatches, stock, etc.) — for a theme section sourcing its products from a Collection.</summary>
    Task<IReadOnlyList<ProductListItemDto>> MembersForStorefrontAsync(long id, int limit, CancellationToken ct = default);
    Task SetManualMembersAsync(long id, IReadOnlyList<long> productIds, CancellationToken ct = default);
    Task<PublicCollectionDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
}

public sealed class CollectionService(EcommerceDbContext db) : ICollectionService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] Types = { "Manual", "Automated" };

    public async Task<IReadOnlyList<CollectionDto>> ListAsync(CancellationToken ct = default)
    {
        var cols = await db.Collections.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync(ct);
        var result = new List<CollectionDto>();
        foreach (var c in cols)
            result.Add(ToDto(c, await CountAsync(c, ct)));
        return result;
    }

    public async Task<CollectionDto> GetAsync(long id, CancellationToken ct = default)
    {
        var c = await db.Collections.FirstOrDefaultAsync(x => x.CollectionId == id, ct) ?? throw NotFound();
        return ToDto(c, await CountAsync(c, ct));
    }

    public async Task<CollectionDto> CreateAsync(SaveCollectionRequest req, CancellationToken ct = default)
    {
        var c = new Collection { CreatedAt = DateTime.UtcNow };
        Apply(c, req);
        c.Slug = await UniqueSlugAsync(req.Slug ?? req.Name, null, ct);
        db.Collections.Add(c);
        await db.SaveChangesAsync(ct);
        return ToDto(c, await CountAsync(c, ct));
    }

    public async Task<CollectionDto> UpdateAsync(long id, SaveCollectionRequest req, CancellationToken ct = default)
    {
        var c = await db.Collections.FirstOrDefaultAsync(x => x.CollectionId == id, ct) ?? throw NotFound();
        Apply(c, req);
        c.Slug = await UniqueSlugAsync(req.Slug ?? req.Name, id, ct);
        c.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(c, await CountAsync(c, ct));
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var c = await db.Collections.FirstOrDefaultAsync(x => x.CollectionId == id, ct) ?? throw NotFound();
        db.ProductCollections.RemoveRange(await db.ProductCollections.Where(p => p.CollectionId == id).ToListAsync(ct));
        db.Collections.Remove(c);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CollectionProductDto>> MembersAsync(long id, bool activeOnly, CancellationToken ct = default)
    {
        var c = await db.Collections.FirstOrDefaultAsync(x => x.CollectionId == id, ct) ?? throw NotFound();
        return await ProjectAsync(MembersQuery(c, activeOnly), ct);
    }

    public async Task<IReadOnlyList<ProductListItemDto>> MembersForStorefrontAsync(long id, int limit, CancellationToken ct = default)
    {
        var c = await db.Collections.FirstOrDefaultAsync(x => x.CollectionId == id, ct) ?? throw NotFound();
        return await MembersQuery(c, activeOnly: true)
            .OrderByDescending(p => p.IsFeatured).ThenBy(p => p.ProductId)
            .Take(limit)
            .Select(p => new ProductListItemDto(
                p.ProductId, p.Sku, p.Name, p.Slug, p.Price, p.CompareAtPrice, p.Status, p.IsFeatured,
                p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                p.Category!.Name,
                p.Brand != null ? p.Brand.Name : null,
                p.InventoryRecords.Sum(i => i.AvailableQty) > 0,
                p.InventoryRecords.Sum(i => i.AvailableQty),
                p.InventoryRecords.Any(i => i.ReorderLevel > 0 && i.AvailableQty <= i.ReorderLevel),
                p.Variants.SelectMany(v => v.Options).Where(o => o.OptionName == "Color").Select(o => o.OptionValue).Distinct().ToList(),
                p.CreatedAt,
                p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.Url).Skip(1).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task SetManualMembersAsync(long id, IReadOnlyList<long> productIds, CancellationToken ct = default)
    {
        var c = await db.Collections.FirstOrDefaultAsync(x => x.CollectionId == id, ct) ?? throw NotFound();
        if (c.CollectionType != "Manual") throw new AppException("Only manual collections have a fixed product list.");
        db.ProductCollections.RemoveRange(await db.ProductCollections.Where(p => p.CollectionId == id).ToListAsync(ct));
        var order = 0;
        foreach (var pid in productIds.Distinct())
            db.ProductCollections.Add(new ProductCollection { CollectionId = id, ProductId = pid, DisplayOrder = order++ });
        await db.SaveChangesAsync(ct);
    }

    public async Task<PublicCollectionDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var c = await db.Collections.FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive, ct);
        if (c is null) return null;
        var products = await ProjectAsync(MembersQuery(c, activeOnly: true), ct);
        return new PublicCollectionDto(c.CollectionId, c.Name, c.Slug, c.Description, c.ImageUrl, c.MetaTitle, c.MetaDescription, products);
    }

    // ---- membership resolution ----
    private IQueryable<Product> MembersQuery(Collection c, bool activeOnly)
    {
        IQueryable<Product> baseQ = db.Products.Where(p => !p.IsDeleted);
        if (activeOnly) baseQ = baseQ.Where(p => p.IsActive && p.Status == "Active");

        if (c.CollectionType == "Manual")
        {
            var ids = db.ProductCollections.Where(pc => pc.CollectionId == c.CollectionId).Select(pc => pc.ProductId);
            return baseQ.Where(p => ids.Contains(p.ProductId));
        }

        var rules = ParseRules(c.RulesJson);
        if (rules.Count == 0) return baseQ.Where(_ => false);   // an automated collection with no rules matches nothing

        if (c.MatchType == "Any")
        {
            IQueryable<Product>? union = null;
            foreach (var rule in rules)
            {
                var q = baseQ.Where(Predicate(rule));
                union = union is null ? q : union.Union(q);
            }
            return union!;
        }

        var all = baseQ;
        foreach (var rule in rules) all = all.Where(Predicate(rule));
        return all;
    }

    /// <summary>Translate one rule into an EF predicate over Product.</summary>
    private static Expression<Func<Product, bool>> Predicate(CollectionRule rule)
    {
        var v = rule.Value?.Trim() ?? "";
        switch (rule.Field.ToLowerInvariant())
        {
            case "category":
                return long.TryParse(v, out var cat) ? p => p.CategoryId == cat : _ => false;
            case "type":
                return p => p.ProductType == v;
            case "title":
                return p => p.Name.Contains(v);
            case "tag":
                return p => p.Tags != null && p.Tags.Contains(v);
            case "featured":
                return p => p.IsFeatured;
            case "price":
                if (!decimal.TryParse(v, out var price)) return _ => false;
                return rule.Op.ToLowerInvariant() switch
                {
                    "gte" => p => p.Price >= price,
                    "lte" => p => p.Price <= price,
                    "eq" => p => p.Price == price,
                    _ => _ => false,
                };
            default: return _ => false;
        }
    }

    private Task<int> CountAsync(Collection c, CancellationToken ct) => MembersQuery(c, activeOnly: false).CountAsync(ct);

    private static Task<List<CollectionProductDto>> ProjectAsync(IQueryable<Product> q, CancellationToken ct) =>
        q.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.ProductId)
            .Select(p => new CollectionProductDto(p.ProductId, p.Name, p.Slug, p.Price,
                p.Images.OrderByDescending(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault()))
            .ToListAsync(ct);

    // ---- helpers ----
    private void Apply(Collection c, SaveCollectionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Name is required.");
        var type = Types.FirstOrDefault(t => t.Equals(req.CollectionType, StringComparison.OrdinalIgnoreCase))
            ?? throw new AppException("Type must be Manual or Automated.");
        c.Name = req.Name.Trim();
        c.Description = req.Description?.Trim();
        c.ImageUrl = req.ImageUrl?.Trim();
        c.CollectionType = type;
        c.MatchType = string.Equals(req.MatchType, "Any", StringComparison.OrdinalIgnoreCase) ? "Any" : "All";
        c.RulesJson = type == "Automated" && req.Rules is { Count: > 0 } ? JsonSerializer.Serialize(req.Rules, Json) : null;
        c.MetaTitle = req.MetaTitle?.Trim();
        c.MetaDescription = req.MetaDescription?.Trim();
        c.IsActive = req.IsActive;
    }

    private static List<CollectionRule> ParseRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<CollectionRule>>(json, Json) ?? []; }
        catch { return []; }
    }

    private CollectionDto ToDto(Collection c, int count) => new(
        c.CollectionId, c.Name, c.Slug, c.Description, c.ImageUrl, c.CollectionType, c.MatchType,
        ParseRules(c.RulesJson), c.MetaTitle, c.MetaDescription, c.IsActive, count);

    private async Task<string> UniqueSlugAsync(string source, long? id, CancellationToken ct)
    {
        var baseSlug = System.Text.RegularExpressions.Regex.Replace(source.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "collection";
        var slug = baseSlug; var n = 1;
        while (await db.Collections.AnyAsync(c => c.Slug == slug && c.CollectionId != id, ct))
            slug = $"{baseSlug}-{++n}";
        return slug;
    }

    private static AppException NotFound() => new("Collection not found.", StatusCodes.Status404NotFound);
}
