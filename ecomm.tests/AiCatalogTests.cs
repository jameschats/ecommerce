using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class AiCatalogTests
{
    private sealed class CannedAi(string response) : IAiService
    {
        public bool Enabled => true;
        public long EstimateCostMicros(AiCompletion completion) => 0;
        public Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
            => Task.FromResult(new AiCompletion(response, 100, 800, "test-model"));
    }

    private const string CatalogJson = """
    {"categories":[
      {"name":"Apparel","description":"Clothing","subcategories":[
        {"name":"Men","description":"Men's wear","products":[{"name":"Blue Tee","shortDescription":"soft tee","description":"A soft cotton tee.","price":499,"tags":"tee,summer"}]},
        {"name":"Women","description":"Women's wear","products":[{"name":"Red Dress","shortDescription":"light dress","description":"A light summer dress.","price":999,"tags":"dress"}]}
      ]},
      {"name":"Accessories","description":"Add-ons","products":[{"name":"Canvas Cap","shortDescription":"cap","description":"A canvas cap.","price":299,"tags":"cap"}]}
    ]}
    """;

    // "Rugged Boot" carries a stray numeric + boolean attribute value despite the prompt asking for plain
    // strings only — models do this in practice; MapAttributes must tolerate it instead of throwing and
    // burning the 25-credit generation over one stray field. "Trail Cap" repeats the "Brand" field so the
    // dedup/reuse path (one AttributeDefinition, not two) is exercised too.
    private const string CatalogJsonWithAttributes = """
    {"categories":[
      {"name":"Footwear","description":"Shoes","products":[
        {"name":"Rugged Boot","shortDescription":"boot","description":"A rugged boot.","price":1999,"tags":"boot",
         "attributes":{"Brand":"Trailwalk","Size":"9","Warranty":12,"Waterproof":true,"Extras":["laces","insole"]}},
        {"name":"Trail Cap","shortDescription":"cap","description":"A trail cap.","price":399,"tags":"cap",
         "attributes":{"Brand":"Trailwalk","Material":"Cotton"}}
      ]}
    ]}
    """;

    private static AiCatalogService New(EcommerceDbContext db) => New(db, CatalogJson);

    private static AiCatalogService New(EcommerceDbContext db, string json)
    {
        var credits = new AiCreditService(db, new CannedAi(json), new ecomm.api.Features.Ai.NullImageAiService(), new HttpContextAccessor());
        return new AiCatalogService(db, credits);
    }

    private static void SeedPlan(EcommerceDbContext db, int credits = 100)
    {
        db.Plans.Add(new Plan { PlanId = 1, Name = "P", Slug = "p", AiCredits = credits, IsActive = true });
        db.TenantSubscriptions.Add(new TenantSubscription { PlanId = 1, Status = "Trial", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    [Fact]
    public async Task Generate_parses_tree_assigns_images_and_debits_25()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db);

        var catalog = await New(db).GenerateAsync(new GenerateCatalogRequest("fashion", null, 6, 6));

        Assert.Equal(2, catalog.Categories.Count);
        var apparel = catalog.Categories[0];
        Assert.Equal("Apparel", apparel.Name);
        Assert.NotNull(apparel.Subcategories);
        Assert.Equal(2, apparel.Subcategories!.Count);                       // Men + Women
        Assert.All(catalog.Categories.SelectMany(c => (c.Subcategories ?? new List<GenCategory>()).SelectMany(s => s.Products).Concat(c.Products)),
            p => Assert.False(string.IsNullOrWhiteSpace(p.ImageUrl)));       // every product got a photo
        Assert.Contains(await db.AiUsageLogs.ToListAsync(), l => l.Feature == AiCreditPricing.SampleCatalog && l.Credits == -25);
    }

    [Fact]
    public async Task Seed_writes_category_tree_products_with_ai_skus_and_stock()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db);
        var svc = New(db);
        var catalog = await svc.GenerateAsync(new GenerateCatalogRequest("fashion", null, 6, 6));

        var result = await svc.SeedAsync(catalog);

        Assert.Equal(2, result.Categories);        // top-level count
        Assert.Equal(3, result.Products);          // Blue Tee + Red Dress + Canvas Cap
        Assert.Equal(4, await db.Categories.CountAsync());   // 2 top + 2 sub
        Assert.Equal(2, await db.Categories.CountAsync(c => c.ParentCategoryId != null));   // Men + Women
        var products = await db.Products.Include(p => p.Images).Include(p => p.InventoryRecords).ToListAsync();
        Assert.Equal(3, products.Count);
        Assert.All(products, p => Assert.StartsWith("AI-", p.Sku));
        Assert.All(products, p => Assert.Equal("Active", p.Status));
        Assert.All(products, p => Assert.Single(p.Images));
        Assert.All(products, p => Assert.Equal(25, p.InventoryRecords.Single().AvailableQty));
        // Every seeded category (top + sub) gets a representative photo so the shop-by-category view isn't blank.
        Assert.All(await db.Categories.ToListAsync(), c => Assert.False(string.IsNullOrWhiteSpace(c.ImageUrl)));
    }

    [Fact]
    public async Task Reseeding_reuses_existing_categories_by_name_but_still_creates_fresh_products()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db);
        var svc = New(db);
        await svc.SeedAsync(await svc.GenerateAsync(new GenerateCatalogRequest("fashion", null, 6, 6)));

        var result2 = await svc.SeedAsync(await svc.GenerateAsync(new GenerateCatalogRequest("fashion", null, 6, 6)));

        Assert.Equal(3, result2.Products);
        Assert.Equal(4, await db.Categories.CountAsync());   // still 2 top + 2 sub — no duplicate "Apparel" etc.
        Assert.Equal(6, await db.Products.CountAsync());     // products are not deduped: 3 + 3

        var apparelCats = await db.Categories.Where(c => c.Name == "Apparel").ToListAsync();
        Assert.Single(apparelCats);                           // reused, not duplicated
        var men = await db.Categories.SingleAsync(c => c.Name == "Men");
        Assert.Equal(2, await db.Products.CountAsync(p => p.CategoryId == men.CategoryId));   // both runs' Blue Tee landed here
    }

    [Fact]
    public async Task Seeding_does_not_throw_when_pre_existing_categories_already_have_duplicate_names()
    {
        // Reproduces a live crash: stores that hit the old (pre-dedup) bug already have same-name
        // categories from before this fix shipped. GroupBy+First must be used instead of ToDictionary,
        // or a second same-name category throws "An item with the same key has already been added."
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db);
        var now = DateTime.UtcNow;
        db.Categories.AddRange(
            new Category { Name = "Apparel", Slug = "apparel", DisplayOrder = 0, IsActive = true, CreatedAt = now },
            new Category { Name = "Apparel", Slug = "apparel-2", DisplayOrder = 1, IsActive = true, CreatedAt = now });
        await db.SaveChangesAsync();
        var svc = New(db);

        var result = await svc.SeedAsync(await svc.GenerateAsync(new GenerateCatalogRequest("fashion", null, 6, 6)));

        Assert.Equal(3, result.Products);
        Assert.Equal(2, await db.Categories.CountAsync(c => c.Name == "Apparel"));   // still 2 — no third created
        Assert.Equal(5, await db.Categories.CountAsync());                          // 2 Apparel (pre-existing) + Men + Women + Accessories
    }

    [Fact]
    public async Task Clear_removes_only_ai_products_and_their_children()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db);
        var svc = New(db);
        var catalog = await svc.GenerateAsync(new GenerateCatalogRequest("fashion", null, 6, 6));
        await svc.SeedAsync(catalog);

        var removed = await svc.ClearAsync();

        Assert.Equal(3, removed);
        Assert.Equal(0, await db.Products.CountAsync());
        Assert.Equal(0, await db.ProductImages.CountAsync());
        Assert.Equal(4, await db.Categories.CountAsync());   // categories are intentionally left in place
    }

    [Fact]
    public async Task SampleCount_counts_ai_prefixed_products()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db);
        var svc = New(db);
        await svc.SeedAsync(await svc.GenerateAsync(new GenerateCatalogRequest("fashion", null, 6, 6)));

        Assert.Equal(3, await svc.SampleCountAsync());
    }

    [Fact]
    public async Task Generate_tolerates_non_string_attribute_values_instead_of_failing_the_whole_response()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db);
        var svc = New(db, CatalogJsonWithAttributes);

        var catalog = await svc.GenerateAsync(new GenerateCatalogRequest("footwear", null, 6, 6));

        var boot = catalog.Categories.Single().Products.Single(p => p.Name == "Rugged Boot");
        Assert.NotNull(boot.Attributes);
        Assert.Equal("Trailwalk", boot.Attributes!["Brand"]);
        Assert.Equal("9", boot.Attributes["Size"]);
        Assert.Equal("12", boot.Attributes["Warranty"]);      // stringified number, not thrown away
        Assert.Equal("Yes", boot.Attributes["Waterproof"]);   // stringified bool
        Assert.False(boot.Attributes.ContainsKey("Extras"));  // array value dropped, not a crash
    }

    [Fact]
    public async Task Seed_writes_product_attributes_and_reuses_one_definition_across_products()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db);
        var svc = New(db, CatalogJsonWithAttributes);

        await svc.SeedAsync(await svc.GenerateAsync(new GenerateCatalogRequest("footwear", null, 6, 6)));

        // "Brand" appears on both products but must exist as exactly one tenant-scoped AttributeDefinition.
        var brandDefs = await db.Attributes.Where(a => a.Code == "brand").ToListAsync();
        Assert.Single(brandDefs);
        Assert.Equal("string", brandDefs[0].DataType);

        var boot = await db.Products.SingleAsync(p => p.Name == "Rugged Boot");
        var bootAttrs = await db.ProductAttributeValues.Include(v => v.Attribute)
            .Where(v => v.ProductId == boot.ProductId).ToListAsync();
        Assert.Equal(4, bootAttrs.Count);   // Brand, Size, Warranty, Waterproof (Extras was dropped)
        Assert.Contains(bootAttrs, v => v.Attribute!.Name == "Brand" && v.ValueText == "Trailwalk");
        Assert.Contains(bootAttrs, v => v.Attribute!.Name == "Waterproof" && v.ValueText == "Yes");

        var cap = await db.Products.SingleAsync(p => p.Name == "Trail Cap");
        var capBrand = await db.ProductAttributeValues.Include(v => v.Attribute)
            .SingleAsync(v => v.ProductId == cap.ProductId && v.Attribute!.Name == "Brand");
        Assert.Equal(brandDefs[0].AttributeId, capBrand.AttributeId);   // same definition, not a duplicate
    }

    [Fact]
    public async Task Seed_writes_no_attribute_rows_when_the_ai_returned_none()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedPlan(db);
        var svc = New(db);   // CatalogJson has no "attributes" key on any product

        await svc.SeedAsync(await svc.GenerateAsync(new GenerateCatalogRequest("fashion", null, 6, 6)));

        Assert.Equal(0, await db.ProductAttributeValues.CountAsync());
        Assert.Equal(0, await db.Attributes.CountAsync());
    }
}
