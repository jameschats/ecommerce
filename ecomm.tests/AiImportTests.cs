using System.Text;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Catalog.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

public class AiImportTests
{
    private sealed class CannedAi(string response) : IAiService
    {
        public bool Enabled => true;
        public long EstimateCostMicros(AiCompletion completion) => 0;
        public Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
            => Task.FromResult(new AiCompletion(response, 40, 20, "test-model"));
    }

    private const string Csv = "Item Code,Title,Dept,MRP\nA1,Blue Tee,Apparel,499\nA2,Red Cap,Apparel,299\n";
    private const string MappingJson = """{"mapping":{"Item Code":"sku","Title":"name","Dept":"category","MRP":"price"}}""";

    private static AiImportService New(EcommerceDbContext db, string aiResponse)
    {
        db.Plans.Add(new Plan { PlanId = 1, Name = "P", Slug = "p", AiCredits = 100, IsActive = true });
        db.TenantSubscriptions.Add(new TenantSubscription { PlanId = 1, Status = "Trial", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        var ai = new CannedAi(aiResponse);
        var credits = new AiCreditService(db, ai, new ecomm.api.Features.Ai.NullImageAiService(), new HttpContextAccessor());
        return new AiImportService(db, credits, ai, new ProductImportService(db, new ecomm.api.Features.Plans.EntitlementService(db)));
    }

    private static MemoryStream CsvStream() => new(Encoding.UTF8.GetBytes(Csv));

    [Fact]
    public async Task Analyze_maps_columns_and_debits_two_credits()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, MappingJson);

        var a = await svc.AnalyzeAsync(CsvStream(), "products.csv");

        Assert.Equal(new[] { "Item Code", "Title", "Dept", "MRP" }, a.Headers);
        Assert.Equal("sku", a.Mapping["Item Code"]);
        Assert.Equal("name", a.Mapping["Title"]);
        Assert.Equal("category", a.Mapping["Dept"]);
        Assert.Equal("price", a.Mapping["MRP"]);
        Assert.Equal(2, a.SampleRows.Count);
        Assert.Contains(await db.AiUsageLogs.ToListAsync(), l => l.Feature == AiCreditPricing.ColumnMap && l.Credits == -2);
    }

    [Fact]
    public async Task Analyze_defaults_unknown_targets_to_ignore()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, """{"mapping":{"Item Code":"sku","Title":"name","Dept":"nonsense","MRP":"price"}}""");

        var a = await svc.AnalyzeAsync(CsvStream(), "products.csv");

        Assert.Equal("ignore", a.Mapping["Dept"]);   // invalid target → ignore
    }

    [Fact]
    public async Task Apply_transforms_creates_categories_and_imports()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, MappingJson);
        var mapping = new Dictionary<string, string>
        {
            ["Item Code"] = "sku", ["Title"] = "name", ["Dept"] = "category", ["MRP"] = "price",
        };

        var result = await svc.ApplyAsync(CsvStream(), "products.csv", mapping, userId: null);

        Assert.Equal(2, result.Job.SuccessRows);
        Assert.Equal(0, result.Job.FailedRows);
        Assert.True(await db.Categories.AnyAsync(c => c.Name == "Apparel"));   // auto-created
        var tee = await db.Products.FirstOrDefaultAsync(p => p.Sku == "A1");
        Assert.NotNull(tee);
        Assert.Equal("Blue Tee", tee!.Name);
        Assert.Equal(499m, tee.Price);
    }
}
