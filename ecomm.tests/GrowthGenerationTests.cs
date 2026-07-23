using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Growth;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// AI Growth text generation (G1). The model is stubbed — what's under test is that the prompt
/// carries the brand voice and real product facts, that output lands in the library, and that email
/// splits into subject + body.
/// </summary>
public class GrowthGenerationTests
{
    private static (EcommerceDbContext db, GrowthGenerationService svc, CapturingAi ai) Setup()
    {
        var db = TestDb.New(tenantId: 1);
        db.Categories.Add(new Category { CategoryId = 1, TenantId = 1, Name = "Sarees", Slug = "sarees", CreatedAt = DateTime.UtcNow });
        db.Products.Add(new Product
        {
            ProductId = 9, TenantId = 1, CategoryId = 1, Name = "Kanchipuram Silk Saree",
            Slug = "kanchipuram-silk", Sku = "KS1", Price = 4999m,
            ShortDescription = "Handwoven pure silk", CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        var ai = new CapturingAi();
        var svc = new GrowthGenerationService(db, new PassThroughCredits(ai), new BrandKitService(db));
        return (db, svc, ai);
    }

    [Fact]
    public async Task Generating_uses_product_facts_and_saves_to_the_library()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;

        var result = await svc.GenerateAsync(new GenerateRequest("instagram-caption", 9, null, null), userId: 1);

        Assert.Contains("Kanchipuram Silk Saree", ai.LastPrompt!.User);
        Assert.Contains("₹4999", ai.LastPrompt.User);
        Assert.Contains("Handwoven pure silk", ai.LastPrompt.User);

        var saved = await db.GrowthContents.SingleAsync();
        Assert.Equal("instagram-caption", saved.ContentType);
        Assert.Equal(9, saved.ProductId);
        Assert.Equal("Draft", saved.Status);
        Assert.Equal(result.Id, saved.GrowthContentId);
    }

    [Fact]
    public async Task The_brand_kit_shapes_the_system_prompt()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;
        await new BrandKitService(db).SaveAsync(new BrandKitDto(
            "premium", "Hinglish", "brides in Tamil Nadu", UseEmoji: false, Hashtags: "#SilkSaree", DoNotSay: "cheap, discount"));

        await svc.GenerateAsync(new GenerateRequest("facebook-post", 9, null, null), 1);

        var sys = ai.LastPrompt!.System;
        Assert.Contains("Tone: premium", sys);
        Assert.Contains("Hinglish", sys);
        Assert.Contains("brides in Tamil Nadu", sys);
        Assert.Contains("Emoji: none", sys);
        Assert.Contains("cheap, discount", sys);
    }

    [Fact]
    public async Task An_explicit_language_overrides_the_brand_kit_default()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;   // brand kit default is English

        var result = await svc.GenerateAsync(new GenerateRequest("whatsapp", 9, "Tamil", null), 1);

        Assert.Contains("Write it in Tamil", ai.LastPrompt!.User);
        Assert.Equal("Tamil", result.Language);
    }

    [Fact]
    public async Task Email_splits_into_subject_and_body()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;
        ai.Reply = "SUBJECT: Diwali silk, 20% off\n\nHi there,\nOur festive collection is live.\nShop now!";

        var result = await svc.GenerateAsync(new GenerateRequest("email", 9, null, null), 1);

        Assert.Equal("Diwali silk, 20% off", result.Title);
        Assert.StartsWith("Hi there,", result.Body);
        Assert.DoesNotContain("SUBJECT:", result.Body);
    }

    [Fact]
    public async Task A_product_type_without_a_product_is_rejected_and_charges_nothing()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;

        await Assert.ThrowsAsync<AppException>(
            () => svc.GenerateAsync(new GenerateRequest("instagram-caption", null, null, null), 1));
        Assert.Null(ai.LastPrompt);   // never reached the model
        Assert.Empty(await db.GrowthContents.ToListAsync());
    }

    [Fact]
    public async Task A_festival_offer_needs_no_product_and_uses_the_brief()
    {
        var (db, svc, ai) = Setup();
        using var _ = db;

        var result = await svc.GenerateAsync(
            new GenerateRequest("festival-offer", null, null, "Pongal - flat ₹500 off silk"), 1);

        Assert.Contains("Pongal - flat ₹500 off silk", ai.LastPrompt!.User);
        Assert.Null(result.ProductId);
    }

    [Fact]
    public async Task Unknown_content_type_is_rejected()
    {
        var (db, svc, _) = Setup();
        using var _db = db;

        await Assert.ThrowsAsync<AppException>(
            () => svc.GenerateAsync(new GenerateRequest("tiktok-dance", 9, null, null), 1));
    }

    [Fact]
    public async Task The_library_hides_discarded_content_and_filters_by_type()
    {
        var (db, svc, _) = Setup();
        using var _db = db;
        await svc.GenerateAsync(new GenerateRequest("instagram-caption", 9, null, null), 1);
        var fb = await svc.GenerateAsync(new GenerateRequest("facebook-post", 9, null, null), 1);
        await svc.UpdateAsync(fb.Id, "edited", null, "Discarded");

        Assert.Equal(1, (await svc.LibraryAsync(null, null, 1, 20)).TotalCount);          // discarded hidden
        Assert.Equal(1, (await svc.LibraryAsync("instagram-caption", null, 1, 20)).TotalCount);
        Assert.Equal(0, (await svc.LibraryAsync("email", null, 1, 20)).TotalCount);
    }

    [Fact]
    public void Every_type_has_a_priced_credit_cost()
    {
        var (db, svc, _) = Setup();
        using var _ = db;

        Assert.All(svc.Types(), t => Assert.True(t.Credits > 0, $"{t.Key} has no credit cost"));
    }

    private sealed class CapturingAi : IAiService
    {
        public AiPrompt? LastPrompt { get; private set; }
        public string Reply { get; set; } = "Generated marketing copy.";
        public bool Enabled => true;
        public long EstimateCostMicros(AiCompletion completion) => 0;

        public Task<AiCompletion> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
        {
            LastPrompt = prompt;
            return Task.FromResult(new AiCompletion(Reply, 100, 50, "test-model"));
        }
    }

    private sealed class PassThroughCredits(IAiService ai) : IAiCreditService
    {
        public async Task<T> MeterAsync<T>(string feature, Func<IAiService, Task<(T, AiCompletion)>> action, CancellationToken ct = default)
        {
            var (result, _) = await action(ai);
            return result;
        }

        public Task<AiBalanceDto> GetBalanceAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiUsageDto>> GetUsageAsync(int take = 50, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AiCreditPack?> GetPackAsync(int packId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> TopUpAsync(int packId, string reference, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
