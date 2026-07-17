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

    private static AiCatalogService New(EcommerceDbContext db)
    {
        var credits = new AiCreditService(db, new CannedAi(CatalogJson), new HttpContextAccessor());
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
}
