using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using ecomm.api.Features.Plans;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// Faceted storefront browse (PLP overhaul). Covers the new filters (attributes, colour, size, stock,
/// sale, rating, category subtree), rating on the card, the facet counts, and — the part that carries the
/// UX — per-facet exclusion, which is what lets a shopper multi-select within one facet.
/// </summary>
public class ProductFacetTests
{
    private static ProductService NewService(EcommerceDbContext db) => new(db, new EntitlementService(db));

    /// <summary>
    /// Seeds a small apparel catalog: parent "Apparel" → child "Sarees"; two brands; a filterable
    /// "Fabric" attribute (Silk/Cotton); products with colour/size variants, stock, sale prices, reviews.
    /// </summary>
    private static EcommerceDbContext SeedCatalog()
    {
        var db = TestDb.New(tenantId: 1);
        var now = DateTime.UtcNow;

        db.Categories.Add(new Category { CategoryId = 1, TenantId = 1, Name = "Apparel", Slug = "apparel", IsActive = true, CreatedAt = now });
        db.Categories.Add(new Category { CategoryId = 2, TenantId = 1, Name = "Sarees", Slug = "sarees", ParentCategoryId = 1, IsActive = true, CreatedAt = now });
        db.Brands.Add(new Brand { BrandId = 1, TenantId = 1, Name = "Nalli", Slug = "nalli", IsActive = true, CreatedAt = now });
        db.Brands.Add(new Brand { BrandId = 2, TenantId = 1, Name = "Fabindia", Slug = "fabindia", IsActive = true, CreatedAt = now });

        db.Attributes.Add(new AttributeDefinition { AttributeId = 1, TenantId = 1, Name = "Fabric", Code = "fabric", DataType = "string", IsFilterable = true, IsActive = true });
        db.AttributeValues.Add(new AttributeValue { AttributeValueId = 1, AttributeId = 1, Value = "Silk" });
        db.AttributeValues.Add(new AttributeValue { AttributeValueId = 2, AttributeId = 1, Value = "Cotton" });

        // P1: Sarees / Nalli / Silk / Blue+Red / in stock / on sale / 5★
        AddProduct(db, 1, category: 2, brand: 1, name: "Blue Silk Saree", price: 3000m, compareAt: 4000m, fabricValueId: 1,
            colors: new[] { "Blue", "Red" }, sizes: new[] { "Free" }, stock: 5, ratings: new byte[] { 5, 5 });
        // P2: Sarees / Fabindia / Cotton / Green / out of stock / no sale / 3★
        AddProduct(db, 2, category: 2, brand: 2, name: "Green Cotton Saree", price: 1200m, compareAt: null, fabricValueId: 2,
            colors: new[] { "Green" }, sizes: new[] { "Free" }, stock: 0, ratings: new byte[] { 3 });
        // P3: Apparel (parent) / Nalli / Silk / Blue / in stock / on sale / no reviews
        AddProduct(db, 3, category: 1, brand: 1, name: "Blue Silk Dupatta", price: 800m, compareAt: 1000m, fabricValueId: 1,
            colors: new[] { "Blue" }, sizes: new[] { "S", "M" }, stock: 10, ratings: Array.Empty<byte>());

        db.SaveChanges();
        return db;
    }

    private static void AddProduct(EcommerceDbContext db, long id, long category, long brand, string name,
        decimal price, decimal? compareAt, long fabricValueId, string[] colors, string[] sizes, int stock, byte[] ratings)
    {
        var now = DateTime.UtcNow;
        db.Products.Add(new Product
        {
            ProductId = id, TenantId = 1, CategoryId = category, BrandId = brand, Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'), Sku = $"SKU{id}", Price = price, CompareAtPrice = compareAt,
            Status = "Active", IsActive = true, CreatedAt = now.AddMinutes(id),
        });
        db.ProductAttributeValues.Add(new ProductAttributeValue { ProductId = id, AttributeId = 1, AttributeValueId = fabricValueId });

        var variant = new ProductVariant { ProductVariantId = id * 10, ProductId = id, Sku = $"SKU{id}-V", Name = "V", IsActive = true, CreatedAt = now };
        db.ProductVariants.Add(variant);
        foreach (var c in colors) db.VariantOptions.Add(new VariantOption { ProductVariantId = variant.ProductVariantId, OptionName = "Color", OptionValue = c, CreatedAt = now });
        foreach (var sz in sizes) db.VariantOptions.Add(new VariantOption { ProductVariantId = variant.ProductVariantId, OptionName = "Size", OptionValue = sz, CreatedAt = now });

        db.Inventory.Add(new Inventory { ProductId = id, AvailableQty = stock, ReorderLevel = 2 });
        for (var i = 0; i < ratings.Length; i++)
            db.Reviews.Add(new Review { TenantId = 1, ProductId = id, UserId = 100 + i, Rating = ratings[i], IsApproved = true, CreatedAt = now });
    }

    private static ProductQuery Q(long? category = null, IReadOnlyList<long>? brandIds = null, IReadOnlyList<string>? color = null,
        IReadOnlyList<string>? size = null, IReadOnlyList<string>? attr = null, bool? inStock = null, bool? onSale = null,
        int? minRating = null, string? sort = null, decimal? minPrice = null, decimal? maxPrice = null) =>
        new(null, category, null, null, null, sort, 1, 50, null, minPrice, maxPrice, brandIds, color, size, attr, inStock, onSale, minRating);

    [Fact]
    public async Task Filtering_a_parent_category_includes_child_category_products()
    {
        using var db = SeedCatalog();
        var svc = NewService(db);

        var all = await svc.BrowseAsync(Q(category: 1), adminView: false);   // Apparel = parent
        Assert.Equal(3, all.TotalCount);   // both sarees (child) + the dupatta (parent)

        var sarees = await svc.BrowseAsync(Q(category: 2), adminView: false); // Sarees = child only
        Assert.Equal(2, sarees.TotalCount);
    }

    [Fact]
    public async Task Attribute_filter_narrows_to_matching_products()
    {
        using var db = SeedCatalog();
        var svc = NewService(db);

        var silk = await svc.BrowseAsync(Q(attr: new[] { "fabric:Silk" }), adminView: false);
        Assert.Equal(2, silk.TotalCount);   // P1, P3

        // Multi-select within a facet = OR.
        var both = await svc.BrowseAsync(Q(attr: new[] { "fabric:Silk", "fabric:Cotton" }), adminView: false);
        Assert.Equal(3, both.TotalCount);
    }

    [Fact]
    public async Task Colour_size_brand_stock_sale_and_rating_filters_each_work()
    {
        using var db = SeedCatalog();
        var svc = NewService(db);

        Assert.Equal(2, (await svc.BrowseAsync(Q(color: new[] { "Blue" }), adminView: false)).TotalCount);       // P1, P3
        Assert.Equal(2, (await svc.BrowseAsync(Q(size: new[] { "Free" }), adminView: false)).TotalCount);        // P1, P2
        Assert.Equal(2, (await svc.BrowseAsync(Q(brandIds: new long[] { 1 }), adminView: false)).TotalCount);    // Nalli: P1, P3
        Assert.Equal(2, (await svc.BrowseAsync(Q(inStock: true), adminView: false)).TotalCount);                 // P1, P3 (P2 = 0 stock)
        Assert.Equal(2, (await svc.BrowseAsync(Q(onSale: true), adminView: false)).TotalCount);                  // P1, P3 have compareAt
        Assert.Equal(1, (await svc.BrowseAsync(Q(minRating: 5), adminView: false)).TotalCount);                  // only P1 avg 5
    }

    [Fact]
    public async Task Combined_filters_are_anded_together()
    {
        using var db = SeedCatalog();
        var svc = NewService(db);

        // Silk AND Blue AND in stock → P1 (in stock) + P3 (in stock), both silk+blue.
        var r = await svc.BrowseAsync(Q(attr: new[] { "fabric:Silk" }, color: new[] { "Blue" }, inStock: true), adminView: false);
        Assert.Equal(2, r.TotalCount);
        // Add on-sale under ₹1000 → only P3.
        var r2 = await svc.BrowseAsync(Q(attr: new[] { "fabric:Silk" }, color: new[] { "Blue" }, onSale: true, maxPrice: 1000m), adminView: false);
        Assert.Equal(1, r2.TotalCount);
        Assert.Equal("Blue Silk Dupatta", r2.Items[0].Name);
    }

    [Fact]
    public async Task The_card_carries_rating_and_review_count()
    {
        using var db = SeedCatalog();
        var svc = NewService(db);

        var items = (await svc.BrowseAsync(Q(), adminView: false)).Items;
        var p1 = items.Single(i => i.ProductId == 1);
        Assert.Equal(5, p1.Rating);
        Assert.Equal(2, p1.ReviewCount);
        var p3 = items.Single(i => i.ProductId == 3);
        Assert.Equal(0, p3.Rating);
        Assert.Equal(0, p3.ReviewCount);
    }

    [Fact]
    public async Task Sort_by_rating_and_discount()
    {
        using var db = SeedCatalog();
        var svc = NewService(db);

        var byRating = (await svc.BrowseAsync(Q(sort: "rating"), adminView: false)).Items;
        Assert.Equal(1, byRating[0].ProductId);   // P1 (5★) first

        // Discount %: P3 = 20% (800/1000), P1 = 25% (3000/4000) → P1 first.
        var byDiscount = (await svc.BrowseAsync(Q(sort: "discount"), adminView: false)).Items;
        Assert.Equal(1, byDiscount[0].ProductId);
    }

    [Fact]
    public async Task Facets_report_values_with_counts()
    {
        using var db = SeedCatalog();
        var svc = NewService(db);

        var f = await svc.FacetsAsync(Q());

        Assert.Equal(3, f.Total);
        Assert.Equal(2, f.Brands.Count);
        Assert.Equal(2, f.Brands.Single(b => b.Name == "Nalli").Count);
        Assert.Equal(2, f.Colors.Single(c => c.Value == "Blue").Count);
        var fabric = f.Attributes.Single(a => a.Code == "fabric");
        Assert.Equal(2, fabric.Values.Single(v => v.Value == "Silk").Count);
        Assert.Equal(1, fabric.Values.Single(v => v.Value == "Cotton").Count);
        Assert.Equal(2, f.InStockCount);
        Assert.Equal(2, f.OnSaleCount);
        Assert.Equal(new[] { 3000m, 800m }.Min(), f.PriceMin);
        Assert.Equal(3000m, f.PriceMax);
        Assert.Equal(1, f.RatingCounts[4]);   // one product at 5★
    }

    [Fact]
    public async Task Facet_counts_exclude_their_own_selection_so_multi_select_stays_usable()
    {
        using var db = SeedCatalog();
        var svc = NewService(db);

        // Having selected fabric=Silk, the fabric facet must STILL show Cotton (with its count), so the
        // shopper can add it — that's the per-facet exclusion. Meanwhile brand/colour counts reflect Silk.
        var f = await svc.FacetsAsync(Q(attr: new[] { "fabric:Silk" }));

        var fabric = f.Attributes.Single(a => a.Code == "fabric");
        Assert.Contains(fabric.Values, v => v.Value == "Cotton");          // not zeroed away
        Assert.Equal(2, fabric.Values.Single(v => v.Value == "Silk").Count);
        // Colour facet is filtered by the Silk selection → only Blue (P1, P3) and Red (P1), no Green.
        Assert.DoesNotContain(f.Colors, c => c.Value == "Green");
        Assert.Equal(2, f.Total);   // the result set itself is the two silk products
    }
}
