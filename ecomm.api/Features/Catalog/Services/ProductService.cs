using System.Linq.Expressions;
using ecomm.api.Common;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace ecomm.api.Features.Catalog.Services;

public interface IProductService
{
    Task<PagedResult<ProductListItemDto>> BrowseAsync(ProductQuery query, bool adminView, CancellationToken ct = default);
    Task<FacetsDto> FacetsAsync(ProductQuery query, CancellationToken ct = default);
    Task<ProductDetailDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<ProductDetailDto> CreateAsync(SaveProductRequest req, long? userId, CancellationToken ct = default);
    Task<ProductDetailDto?> UpdateAsync(long id, SaveProductRequest req, long? userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    /// <summary>Products most often bought in the same order as <paramref name="productId"/>, ranked by
    /// co-purchase frequency (real order history, not a manual/curated list). Empty for products with
    /// no qualifying order history yet.</summary>
    Task<List<ProductListItemDto>> GetFrequentlyBoughtTogetherAsync(long productId, int take, CancellationToken ct = default);
    /// <summary>"Trending now": products ranked by recent demand velocity — weighted views + add-to-cart
    /// (behavioural events) and purchases (order history) over a recent window. In-stock only. Empty when
    /// there's not enough recent activity yet (the caller falls back to best-sellers/featured).</summary>
    Task<List<ProductListItemDto>> GetTrendingAsync(int take, int windowDays, CancellationToken ct = default);
}

public sealed class ProductService : IProductService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;
    private readonly ecomm.api.Features.Plans.IEntitlementService _entitlements;
    private readonly ecomm.api.Features.PublicApi.IWebhookDispatchService _webhooks;
    private readonly ILogger<ProductService> _log;
    // Mirrors AnalyticsService's SoldStatuses — an order line counts toward "bestseller" ranking once
    // it's actually been paid/fulfilled, not while still a draft/pending/cancelled/returned order.
    private static readonly string[] SoldStatuses = { "Paid", "Confirmed", "Packed", "Shipped", "Delivered" };

    public ProductService(EcommerceDbContext db, ecomm.api.Features.Plans.IEntitlementService entitlements,
        ecomm.api.Features.PublicApi.IWebhookDispatchService webhooks, ILogger<ProductService> log)
    {
        _db = db;
        _entitlements = entitlements;
        _webhooks = webhooks;
        _log = log;
    }

    private async Task DispatchWebhookAsync(string eventType, object payload, CancellationToken ct)
    {
        try { await _webhooks.DispatchAsync(eventType, payload, ct); }
        catch (Exception ex) { _log.LogError(ex, "Webhook dispatch '{Event}' failed.", eventType); }
    }

    public async Task<PagedResult<ProductListItemDto>> BrowseAsync(ProductQuery query, bool adminView, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var categoryIds = await ExpandCategoryAsync(query.CategoryId, ct);
        var attrFilters = ParseAttrs(query.Attr);

        var q = ApplyFacetFilters(BaseBrowseQuery(query, adminView), query, categoryIds, attrFilters, exclude: null);

        q = query.Sort switch
        {
            "price" => q.OrderBy(p => p.Price),
            "price_desc" => q.OrderByDescending(p => p.Price),
            "name" => q.OrderBy(p => p.Name),
            "rating" => q.OrderByDescending(p =>
                _db.Reviews.Where(r => r.ProductId == p.ProductId && r.IsApproved).Average(r => (double?)r.Rating) ?? 0),
            "discount" => q.OrderByDescending(p =>
                p.CompareAtPrice != null && p.CompareAtPrice > p.Price ? (p.CompareAtPrice.Value - p.Price) / p.CompareAtPrice.Value : 0),
            // All-time total quantity sold (not date-ranged like AnalyticsService.BestSellersAsync,
            // which is a separate admin-report concern) — a correlated subquery since Product has no
            // reverse nav to OrderItem.
            "bestsellers" => q.OrderByDescending(p =>
                _db.OrderItems.Where(oi => oi.ProductId == p.ProductId && SoldStatuses.Contains(oi.Order!.Status))
                    .Sum(oi => (int?)oi.Quantity) ?? 0),
            _ => q.OrderByDescending(p => p.CreatedAt),
        };

        var total = await q.LongCountAsync(ct);
        var items = await q
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(ListItemProjection())
            .ToListAsync(ct);

        return new PagedResult<ProductListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    /// <summary>Base set + visibility + search, before facet filters. Shared by browse and facet counting.</summary>
    private IQueryable<Product> BaseBrowseQuery(ProductQuery query, bool adminView)
    {
        var q = _db.Products.Where(p => p.TenantId == Tenant && !p.IsDeleted);
        if (!adminView) q = q.Where(p => p.IsActive && p.Status == "Active");
        else if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(p => p.Status == query.Status);
        if (query.IsFeatured is { } feat) q = q.Where(p => p.IsFeatured == feat);
        if (query.Ids is { Count: > 0 } ids) q = q.Where(p => ids.Contains(p.ProductId));
        return ApplySearch(q, query.Search);
    }

    private static IQueryable<Product> ApplySearch(IQueryable<Product> q, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return q;
        var s = search.Trim();
        // MySQL FULLTEXT (boolean + prefix) on Name/ShortDescription/Description, with a LIKE fallback
        // for SKUs and short tokens that full-text ignores.
        var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 3).ToList();
        if (tokens.Count > 0)
        {
            var boolQuery = string.Join(' ', tokens.Select(t => $"+{t}*"));
            return q.Where(p =>
                EF.Functions.Match(new[] { p.Name, p.ShortDescription!, p.Description! }, boolQuery, MySqlMatchSearchMode.Boolean) > 0
                || p.Sku.Contains(s) || p.Category!.Name.Contains(s) || (p.Brand != null && p.Brand.Name.Contains(s))
                || p.AttributeValues.Any(av => (av.ValueText != null && av.ValueText.Contains(s)) || (av.Value != null && av.Value.Value.Contains(s))));
        }
        return q.Where(p =>
            p.Name.Contains(s) || p.Sku.Contains(s) || p.Category!.Name.Contains(s) || (p.Brand != null && p.Brand.Name.Contains(s))
            || p.AttributeValues.Any(av => (av.ValueText != null && av.ValueText.Contains(s)) || (av.Value != null && av.Value.Value.Contains(s))));
    }

    /// <summary>
    /// Applies every facet filter except the one named in <paramref name="exclude"/>. That exclusion is
    /// what lets a facet's own counts stay usable for multi-select (choosing "Silk" mustn't zero "Cotton").
    /// Exclude keys: "category", "brand", "price", "color", "size", "stock", "sale", "rating", or "attr:{code}".
    /// </summary>
    private IQueryable<Product> ApplyFacetFilters(
        IQueryable<Product> q, ProductQuery query, IReadOnlyCollection<long> categoryIds,
        IReadOnlyDictionary<string, List<string>> attrFilters, string? exclude)
    {
        if (exclude != "category" && categoryIds.Count > 0)
            q = q.Where(p => categoryIds.Contains(p.CategoryId));

        if (exclude != "brand")
        {
            var brandIds = (query.BrandIds ?? new List<long>()).ToList();
            if (query.BrandId is { } b && !brandIds.Contains(b)) brandIds.Add(b);
            if (brandIds.Count > 0) q = q.Where(p => p.BrandId != null && brandIds.Contains(p.BrandId.Value));
        }
        if (exclude != "price")
        {
            if (query.MinPrice is { } min) q = q.Where(p => p.Price >= min);
            if (query.MaxPrice is { } max) q = q.Where(p => p.Price <= max);
        }
        if (exclude != "color" && query.Color is { Count: > 0 } colors)
            q = q.Where(p => p.Variants.Any(v => v.Options.Any(o => o.OptionName == "Color" && colors.Contains(o.OptionValue))));
        if (exclude != "size" && query.Size is { Count: > 0 } sizes)
            q = q.Where(p => p.Variants.Any(v => v.Options.Any(o => o.OptionName == "Size" && sizes.Contains(o.OptionValue))));

        foreach (var (code, values) in attrFilters)
            if (exclude != "attr:" + code)
                q = q.Where(p => p.AttributeValues.Any(av =>
                    av.Attribute!.Code == code &&
                    ((av.ValueText != null && values.Contains(av.ValueText)) || (av.Value != null && values.Contains(av.Value.Value)))));

        if (exclude != "stock" && query.InStock == true)
            q = q.Where(p => p.InventoryRecords.Sum(i => i.AvailableQty) > 0);
        if (exclude != "sale" && query.OnSale == true)
            q = q.Where(p => p.CompareAtPrice != null && p.CompareAtPrice > p.Price);
        if (exclude != "rating" && query.MinRating is { } mr)
            q = q.Where(p => (_db.Reviews.Where(r => r.ProductId == p.ProductId && r.IsApproved).Average(r => (double?)r.Rating) ?? 0) >= mr);

        return q;
    }

    /// <summary>A category id expands to itself + all descendants, so browsing a parent shows child products.</summary>
    private async Task<IReadOnlyCollection<long>> ExpandCategoryAsync(long? categoryId, CancellationToken ct)
    {
        if (categoryId is not { } root) return Array.Empty<long>();
        var all = await _db.Categories.AsNoTracking()
            .Select(c => new { c.CategoryId, c.ParentCategoryId }).ToListAsync(ct);
        var byParent = all.ToLookup(c => c.ParentCategoryId);
        var result = new List<long>();
        var stack = new Stack<long>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            result.Add(id);
            foreach (var child in byParent[id]) stack.Push(child.CategoryId);
        }
        return result;
    }

    /// <summary>Parse <c>["fabric:Silk","fabric:Cotton","occasion:Wedding"]</c> into code → [values].</summary>
    private static Dictionary<string, List<string>> ParseAttrs(IReadOnlyList<string>? attrs)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in attrs ?? new List<string>())
        {
            var i = raw.IndexOf(':');
            if (i <= 0 || i == raw.Length - 1) continue;
            var code = raw[..i].Trim();
            var value = raw[(i + 1)..].Trim();
            if (code.Length == 0 || value.Length == 0) continue;
            (map.TryGetValue(code, out var list) ? list : map[code] = new List<string>()).Add(value);
        }
        return map;
    }

    /// <summary>Card projection. Instance (not static) so it can carry the review-rating subquery.</summary>
    private Expression<Func<Product, ProductListItemDto>> ListItemProjection() => p => new ProductListItemDto(
        p.ProductId, p.Sku, p.Name, p.Slug, p.Price, p.CompareAtPrice, p.Status, p.IsFeatured,
        p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
        p.Category!.Name,
        p.Brand != null ? p.Brand.Name : null,
        p.InventoryRecords.Sum(i => i.AvailableQty) > 0,
        p.InventoryRecords.Sum(i => i.AvailableQty),
        p.InventoryRecords.Any(i => i.ReorderLevel > 0 && i.AvailableQty <= i.ReorderLevel),
        p.Variants.SelectMany(v => v.Options).Where(o => o.OptionName == "Color").Select(o => o.OptionValue).Distinct().ToList(),
        p.CreatedAt,
        p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.Url).Skip(1).FirstOrDefault(),
        _db.Reviews.Where(r => r.ProductId == p.ProductId && r.IsApproved).Average(r => (double?)r.Rating) ?? 0,
        _db.Reviews.Count(r => r.ProductId == p.ProductId && r.IsApproved));

    /// <summary>
    /// Available filter values + counts for the current result set (the facet rail). Each multi-select
    /// facet is counted with its own selection excluded (see <see cref="ApplyFacetFilters"/>), so a shopper
    /// can pick more than one value in a group.
    /// </summary>
    public async Task<FacetsDto> FacetsAsync(ProductQuery query, CancellationToken ct = default)
    {
        var categoryIds = await ExpandCategoryAsync(query.CategoryId, ct);
        var attrFilters = ParseAttrs(query.Attr);
        var baseQ = BaseBrowseQuery(query, adminView: false);

        // The fully-filtered set drives the toggle counts, price range and total.
        var full = ApplyFacetFilters(baseQ, query, categoryIds, attrFilters, exclude: null);
        var total = await full.CountAsync(ct);
        var priceMin = total == 0 ? 0 : await full.MinAsync(p => p.Price, ct);
        var priceMax = total == 0 ? 0 : await full.MaxAsync(p => p.Price, ct);
        var inStockCount = await full.CountAsync(p => p.InventoryRecords.Sum(i => i.AvailableQty) > 0, ct);
        var onSaleCount = await full.CountAsync(p => p.CompareAtPrice != null && p.CompareAtPrice > p.Price, ct);

        // Facet value counts are computed as "distinct products per value" over the set filtered by all
        // OTHER facets — so each group's own selection doesn't zero its siblings (multi-select). The value
        // aggregation materializes distinct (product, value) pairs then groups in memory: one shape that
        // both MySQL and the in-memory test provider translate cleanly, and correct for count-distinct.

        // Rating buckets (exclude rating).
        var ratingIds = await FacetSetIdsAsync(baseQ, query, categoryIds, attrFilters, "rating", ct);
        var avgByProduct = (await _db.Reviews.AsNoTracking()
                .Where(r => r.IsApproved && ratingIds.Contains(r.ProductId))
                .GroupBy(r => r.ProductId)
                .Select(g => new { g.Key, Avg = g.Average(x => (double)x.Rating) }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Avg);
        var ratingCounts = new int[5];
        for (var star = 1; star <= 5; star++)
            ratingCounts[star - 1] = ratingIds.Count(id => avgByProduct.TryGetValue(id, out var a) && a >= star);

        // Brand facet (exclude brand).
        var brandIdSet = await FacetSetIdsAsync(baseQ, query, categoryIds, attrFilters, "brand", ct);
        var brands = (await _db.Products.AsNoTracking()
                .Where(p => brandIdSet.Contains(p.ProductId) && p.BrandId != null)
                .Select(p => new { Id = p.BrandId!.Value, p.Brand!.Name }).ToListAsync(ct))
            .GroupBy(x => (x.Id, x.Name))
            .Select(g => new BrandFacetDto(g.Key.Id, g.Key.Name, g.Count()))
            .OrderByDescending(b => b.Count).ToList();

        // Colour + size, from variant options — distinct products per value.
        var colorIds = await FacetSetIdsAsync(baseQ, query, categoryIds, attrFilters, "color", ct);
        var colorPairs = await _db.VariantOptions.AsNoTracking()
            .Where(o => o.OptionName == "Color" && colorIds.Contains(o.Variant!.ProductId))
            .Select(o => new { Pid = o.Variant!.ProductId, o.OptionValue }).Distinct().ToListAsync(ct);
        var colors = GroupValues(colorPairs.Select(x => (x.Pid, x.OptionValue)));

        var sizeIds = await FacetSetIdsAsync(baseQ, query, categoryIds, attrFilters, "size", ct);
        var sizePairs = await _db.VariantOptions.AsNoTracking()
            .Where(o => o.OptionName == "Size" && sizeIds.Contains(o.Variant!.ProductId))
            .Select(o => new { Pid = o.Variant!.ProductId, o.OptionValue }).Distinct().ToListAsync(ct);
        var sizes = GroupValues(sizePairs.Select(x => (x.Pid, x.OptionValue)));

        // One attribute facet per filterable attribute, each excluding its own selection.
        var filterable = await _db.Attributes.AsNoTracking()
            .Where(a => a.IsFilterable && a.IsActive)
            .Select(a => new { a.Code, a.Name }).ToListAsync(ct);
        var attributes = new List<AttributeFacetDto>();
        foreach (var a in filterable)
        {
            var code = a.Code;
            var ids = await FacetSetIdsAsync(baseQ, query, categoryIds, attrFilters, "attr:" + code, ct);
            var pairs = await _db.ProductAttributeValues.AsNoTracking()
                .Where(av => av.Attribute!.Code == code && ids.Contains(av.ProductId))
                .Select(av => new { av.ProductId, Val = av.ValueText ?? av.Value!.Value }).Distinct().ToListAsync(ct);
            var values = GroupValues(pairs.Select(x => (x.ProductId, x.Val)));
            if (values.Count > 0) attributes.Add(new AttributeFacetDto(code, a.Name, values));
        }

        return new FacetsDto(total, brands, colors, sizes, attributes, priceMin, priceMax, ratingCounts, inStockCount, onSaleCount);
    }

    /// <summary>Product ids matching every facet except <paramref name="exclude"/>.</summary>
    private async Task<HashSet<long>> FacetSetIdsAsync(
        IQueryable<Product> baseQ, ProductQuery query, IReadOnlyCollection<long> categoryIds,
        IReadOnlyDictionary<string, List<string>> attrFilters, string exclude, CancellationToken ct) =>
        (await ApplyFacetFilters(baseQ, query, categoryIds, attrFilters, exclude)
            .Select(p => p.ProductId).ToListAsync(ct)).ToHashSet();

    /// <summary>Distinct (productId, value) pairs → value facets with distinct-product counts, biggest first.</summary>
    private static List<ValueFacetDto> GroupValues(IEnumerable<(long Pid, string Value)> pairs) =>
        pairs.GroupBy(p => p.Value)
            .Select(g => new ValueFacetDto(g.Key, g.Select(x => x.Pid).Distinct().Count(), null))
            .OrderByDescending(v => v.Count).ThenBy(v => v.Value).ToList();

    public async Task<List<ProductListItemDto>> GetFrequentlyBoughtTogetherAsync(long productId, int take, CancellationToken ct = default)
    {
        // Two-step: materialize the qualifying order ids first, then rank co-purchased products —
        // safer for EF Core/MySQL translation than a single correlated GroupBy+SelectMany query.
        var orderIds = await _db.OrderItems
            .Where(oi => oi.ProductId == productId && SoldStatuses.Contains(oi.Order!.Status))
            .Select(oi => oi.OrderId)
            .Distinct()
            .ToListAsync(ct);
        if (orderIds.Count == 0) return [];

        var rankedIds = await _db.OrderItems
            .Where(oi => orderIds.Contains(oi.OrderId) && oi.ProductId != productId)
            .GroupBy(oi => oi.ProductId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(Math.Clamp(take, 1, 10))
            .ToListAsync(ct);
        if (rankedIds.Count == 0) return [];

        var items = await _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted && p.IsActive && p.Status == "Active" && rankedIds.Contains(p.ProductId))
            .Select(ListItemProjection())
            .ToListAsync(ct);

        var rank = rankedIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return items.OrderBy(i => rank[i.ProductId]).ToList();
    }

    public async Task<List<ProductListItemDto>> GetTrendingAsync(int take, int windowDays, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 24);
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(windowDays, 1, 90));

        // Behavioural signal: weighted views + add-to-cart over the window (tenant-scoped by the filter).
        var evStats = await _db.CustomerEvents
            .Where(e => e.ProductId != null && e.CreatedAt >= since && (e.EventType == "view" || e.EventType == "add-to-cart"))
            .GroupBy(e => new { e.ProductId, e.EventType })
            .Select(g => new { g.Key.ProductId, g.Key.EventType, Count = g.Count() })
            .ToListAsync(ct);

        // Purchase signal: units sold over the window (strongest weight).
        var purchases = await _db.OrderItems
            .Where(oi => SoldStatuses.Contains(oi.Order!.Status) && oi.Order.PlacedAt != null && oi.Order.PlacedAt >= since)
            .GroupBy(oi => oi.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);

        var score = new Dictionary<long, double>();
        foreach (var e in evStats)
            if (e.ProductId is { } pid)
                score[pid] = score.GetValueOrDefault(pid) + e.Count * (e.EventType == "add-to-cart" ? 3.0 : 1.0);
        foreach (var p in purchases)
            score[p.ProductId] = score.GetValueOrDefault(p.ProductId) + p.Qty * 8.0;

        if (score.Count == 0) return [];

        // Over-fetch, then drop anything not live/in-stock, then take the top N (guardrail: never OOS).
        var rankedIds = score.OrderByDescending(kv => kv.Value).Take(take * 3).Select(kv => kv.Key).ToList();
        var items = await _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted && p.IsActive && p.Status == "Active" && rankedIds.Contains(p.ProductId))
            .Select(ListItemProjection())
            .ToListAsync(ct);

        return items.Where(i => i.InStock)
            .OrderByDescending(i => score.GetValueOrDefault(i.ProductId))
            .Take(take)
            .ToList();
    }

    public Task<ProductDetailDto?> GetByIdAsync(long id, CancellationToken ct = default) =>
        Project(_db.Products.Where(p => p.ProductId == id && p.TenantId == Tenant && !p.IsDeleted), ct);

    public Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        Project(_db.Products.Where(p => p.Slug == slug && p.TenantId == Tenant && !p.IsDeleted), ct);

    public async Task<ProductDetailDto> CreateAsync(SaveProductRequest req, long? userId, CancellationToken ct = default)
    {
        Validate(req);
        await _entitlements.EnsureCanAddProductsAsync(1, ct);   // plan ceiling — 402 with an upgrade message
        await EnsureCategoryExists(req.CategoryId, ct);
        var sku = req.Sku.Trim();
        if (await _db.Products.AnyAsync(p => p.TenantId == Tenant && p.Sku == sku, ct))
            throw new AppException($"SKU '{sku}' already exists.", StatusCodes.Status409Conflict);

        var now = DateTime.UtcNow;
        var product = new Product
        {
            TenantId = Tenant,
            Sku = sku,
            Name = req.Name.Trim(),
            Slug = await UniqueSlugAsync(req.Slug ?? req.Name, null, ct),
            CategoryId = req.CategoryId,
            BrandId = req.BrandId,
            ProductType = req.ProductType?.Trim(),
            Tags = NormalizeTags(req.Tags),
            ShortDescription = req.ShortDescription,
            Description = req.Description,
            MetaTitle = req.MetaTitle?.Trim(),
            MetaDescription = req.MetaDescription?.Trim(),
            HsnCode = req.HsnCode,
            Price = req.Price,
            CompareAtPrice = req.CompareAtPrice,
            CostPrice = req.CostPrice,
            Status = NormalizeStatus(req.Status),
            IsFeatured = req.IsFeatured,
            IsBundle = req.IsBundle,
            IsActive = true,
            CreatedBy = userId,
            CreatedAt = now,
            Images = BuildImages(req.Images, now),
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(product.ProductId, ct))!;
    }

    public async Task<ProductDetailDto?> UpdateAsync(long id, SaveProductRequest req, long? userId, CancellationToken ct = default)
    {
        var product = await _db.Products.Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.ProductId == id && p.TenantId == Tenant && !p.IsDeleted, ct);
        if (product is null) return null;

        Validate(req);
        await EnsureCategoryExists(req.CategoryId, ct);
        var sku = req.Sku.Trim();
        if (await _db.Products.AnyAsync(p => p.TenantId == Tenant && p.Sku == sku && p.ProductId != id, ct))
            throw new AppException($"SKU '{sku}' already exists.", StatusCodes.Status409Conflict);

        var now = DateTime.UtcNow;
        product.Sku = sku;
        product.Name = req.Name.Trim();
        product.Slug = await UniqueSlugAsync(req.Slug ?? req.Name, id, ct);
        product.CategoryId = req.CategoryId;
        product.BrandId = req.BrandId;
        product.ProductType = req.ProductType?.Trim();
        product.Tags = NormalizeTags(req.Tags);
        product.ShortDescription = req.ShortDescription;
        product.Description = req.Description;
        product.MetaTitle = req.MetaTitle?.Trim();
        product.MetaDescription = req.MetaDescription?.Trim();
        product.HsnCode = req.HsnCode;
        product.Price = req.Price;
        product.CompareAtPrice = req.CompareAtPrice;
        product.CostPrice = req.CostPrice;
        product.Status = NormalizeStatus(req.Status);
        product.IsFeatured = req.IsFeatured;
        product.IsBundle = req.IsBundle;
        product.UpdatedBy = userId;
        product.UpdatedAt = now;

        if (req.Images is not null)
        {
            _db.ProductImages.RemoveRange(product.Images);
            product.Images = BuildImages(req.Images, now);
        }

        await _db.SaveChangesAsync(ct);
        await DispatchWebhookAsync("product.updated", new
        {
            productId = product.ProductId, sku = product.Sku, name = product.Name,
            status = product.Status, price = product.Price,
        }, ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(
            p => p.ProductId == id && p.TenantId == Tenant && !p.IsDeleted, ct);
        if (product is null) return false;

        product.IsDeleted = true;
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // --- helpers ---

    private static Task<ProductDetailDto?> Project(IQueryable<Product> q, CancellationToken ct) =>
        q.Select(p => new ProductDetailDto(
            p.ProductId, p.Sku, p.Name, p.Slug, p.ShortDescription, p.Description,
            p.Price, p.CompareAtPrice, p.CostPrice, p.HsnCode, p.Status, p.IsFeatured, p.IsActive,
            p.CategoryId, p.Category!.Name, p.BrandId, p.Brand != null ? p.Brand.Name : null,
            p.InventoryRecords.Sum(i => i.AvailableQty), p.InventoryRecords.Sum(i => i.AvailableQty) > 0,
            p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder)
                .Select(i => new ProductImageDto(i.ProductImageId, i.Url, i.AltText, i.DisplayOrder, i.IsPrimary))
                .ToList(),
            p.Variants.OrderBy(v => v.ProductVariantId)
                .Select(v => new ProductVariantDto(v.ProductVariantId, v.Sku, v.Name, v.PriceAdjustment, v.IsActive,
                    v.Options.Select(o => new VariantOptionDto(o.OptionName, o.OptionValue)).ToList()))
                .ToList(),
            p.AttributeValues
                .Select(a => new ProductAttributeValueDto(a.ProductAttributeValueId, a.AttributeId, a.Attribute!.Name,
                    a.AttributeValueId, a.Value != null ? a.Value.Value : null, a.ValueText))
                .ToList(),
            p.ProductType, p.Tags, p.MetaTitle, p.MetaDescription, p.IsBundle))
        .FirstOrDefaultAsync(ct);

    private static string? NormalizeTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags) ? null
        : string.Join(",", tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct());

    private static void Validate(SaveProductRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Sku)) throw new AppException("SKU is required.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Name is required.");
        if (req.Price < 0) throw new AppException("Price cannot be negative.");
    }

    private async Task EnsureCategoryExists(long categoryId, CancellationToken ct)
    {
        if (!await _db.Categories.AnyAsync(c => c.CategoryId == categoryId && c.TenantId == Tenant, ct))
            throw new AppException("The selected category does not exist.");
    }

    private static string NormalizeStatus(string? status) =>
        status is "Active" or "Inactive" or "Draft" ? status : "Draft";

    private static List<ProductImage> BuildImages(IReadOnlyList<ProductImageInput>? images, DateTime now) =>
        (images ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Url))
            .Select(i => new ProductImage
            {
                Url = i.Url.Trim(),
                AltText = i.AltText,
                DisplayOrder = i.DisplayOrder,
                IsPrimary = i.IsPrimary,
                MediaFileId = i.MediaFileId,
                CreatedAt = now,
            })
            .ToList();

    private async Task<string> UniqueSlugAsync(string source, long? excludeId, CancellationToken ct)
    {
        var baseSlug = Slug.From(source);
        var slug = baseSlug;
        var exclude = excludeId ?? 0;
        var n = 1;
        while (await _db.Products.AnyAsync(
            p => p.TenantId == Tenant && p.Slug == slug && p.ProductId != exclude, ct))
        {
            slug = $"{baseSlug}-{++n}";
        }
        return slug;
    }
}
