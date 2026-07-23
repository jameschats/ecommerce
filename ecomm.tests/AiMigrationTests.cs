using System.Text;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>AI-4 platform-migration presets (Shopify/Woo/Wix detection + deterministic mapping).</summary>
public class AiMigrationTests
{
    private sealed class CannedAi(string response) : IAiService
    {
        public bool Enabled => true;
        public long EstimateCostMicros(AiCompletion completion) => 0;
        public Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
            => Task.FromResult(new AiCompletion(response, 20, 10, "test-model"));
    }

    private static AiImportService NewService(EcommerceDbContext db, string aiResponse)
    {
        db.Plans.Add(new Plan { PlanId = 1, Name = "P", Slug = "p", AiCredits = 100, IsActive = true });
        db.TenantSubscriptions.Add(new TenantSubscription { PlanId = 1, Status = "Trial", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        var ai = new CannedAi(aiResponse);
        return new AiImportService(db, new AiCreditService(db, ai, new ecomm.api.Features.Ai.NullImageAiService(), new HttpContextAccessor()), ai, new ProductImportService(db, new ecomm.api.Features.Plans.EntitlementService(db)));
    }

    private static MemoryStream Stream(string csv) => new(Encoding.UTF8.GetBytes(csv));

    [Fact]
    public async Task Shopify_export_is_detected_and_mapped_without_ai()
    {
        const string shopify = "Handle,Title,Body (HTML),Vendor,Type,Variant SKU,Variant Price,Image Src,Status\n"
                             + "blue-tee,Blue Tee,Soft,Acme,Shirts,SKU1,499,http://a.jpg,active\n";
        using var db = TestDb.New(tenantId: 1);
        var svc = NewService(db, "{}");   // AI response unused — the preset covers every column

        var a = await svc.AnalyzeAsync(Stream(shopify), "shopify.csv");

        Assert.Equal("Shopify", a.DetectedFormat);
        Assert.Equal("sku", a.Mapping["Variant SKU"]);
        Assert.Equal("name", a.Mapping["Title"]);
        Assert.Equal("category", a.Mapping["Type"]);
        Assert.Equal("description", a.Mapping["Body (HTML)"]);
        Assert.Equal("imageurl", a.Mapping["Image Src"]);
        Assert.Equal("ignore", a.Mapping["Handle"]);
        Assert.Empty(await db.AiUsageLogs.ToListAsync());   // preset covered everything → no AI, no credits spent
    }

    [Fact]
    public async Task Apply_takes_first_image_from_a_multi_url_cell()
    {
        const string woo = "SKU,Name,Regular price,Categories,Images\nW1,Widget,199,Gadgets,http://a.jpg;http://b.jpg\n";
        using var db = TestDb.New(tenantId: 1);
        var svc = NewService(db, "{}");
        var mapping = new Dictionary<string, string>
        {
            ["SKU"] = "sku", ["Name"] = "name", ["Regular price"] = "price", ["Categories"] = "category", ["Images"] = "imageurl",
        };

        var result = await svc.ApplyAsync(Stream(woo), "woo.csv", mapping, userId: null);

        Assert.Equal(1, result.Job.SuccessRows);
        var images = await db.ProductImages.ToListAsync();
        Assert.Equal("http://a.jpg", Assert.Single(images).Url);   // first of the multi-URL cell
    }
}
