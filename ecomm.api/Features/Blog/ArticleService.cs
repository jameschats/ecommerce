using System.Text.Json;
using ecomm.api.Common;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using ecomm.api.Features.Growth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Blog;

public sealed record ArticleDto(
    long Id, string Title, string Slug, string? Excerpt, string BodyHtml, string? CoverImageUrl,
    string? AuthorName, string? MetaTitle, string? MetaDescription, string Status,
    DateTime? PublishedAt, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record ArticleSummaryDto(
    long Id, string Title, string Slug, string? Excerpt, string? CoverImageUrl, string? AuthorName,
    string Status, DateTime? PublishedAt);

public sealed record SaveArticleRequest(
    string Title, string? Slug, string? Excerpt, string BodyHtml, string? CoverImageUrl,
    string? AuthorName, string? MetaTitle, string? MetaDescription);

public sealed record ArticleDraftRequest(string Topic, string? Language, string? Brief);
public sealed record ArticleDraftDto(string Title, string Excerpt, string BodyHtml, string MetaTitle, string MetaDescription);

public interface IArticleService
{
    // Admin
    Task<PagedResult<ArticleSummaryDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ArticleDto> GetAsync(long id, CancellationToken ct = default);
    Task<ArticleDto> CreateAsync(SaveArticleRequest req, long? userId, CancellationToken ct = default);
    Task<ArticleDto> UpdateAsync(long id, SaveArticleRequest req, CancellationToken ct = default);
    Task<ArticleDto> SetPublishedAsync(long id, bool published, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task<ArticleDraftDto> DraftAsync(ArticleDraftRequest req, CancellationToken ct = default);
    // Public storefront
    Task<PagedResult<ArticleSummaryDto>> PublicListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ArticleDto?> PublicGetBySlugAsync(string slug, CancellationToken ct = default);
}

/// <summary>
/// Blog articles (G5). Admin CRUD + an AI first-draft that writes to the same store the copy tools use,
/// grounded in the brand voice. Only published articles are visible on the storefront. HTML is authored
/// by the store admin (trusted, same model as CMS pages) and rendered as-is on the blog.
/// </summary>
public sealed class ArticleService(
    EcommerceDbContext db, IAiCreditService credits, IBrandKitService brandKit) : IArticleService
{
    public async Task<PagedResult<ArticleSummaryDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = db.Articles.AsNoTracking().OrderByDescending(a => a.ArticleId);
        var total = await q.LongCountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new ArticleSummaryDto(a.ArticleId, a.Title, a.Slug, a.Excerpt, a.CoverImageUrl, a.AuthorName, a.Status, a.PublishedAt))
            .ToListAsync(ct);
        return new PagedResult<ArticleSummaryDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<ArticleDto> GetAsync(long id, CancellationToken ct = default)
        => Map(await Find(id, ct));

    public async Task<ArticleDto> CreateAsync(SaveArticleRequest req, long? userId, CancellationToken ct = default)
    {
        var title = Require(req.Title, "Title");
        var a = new Article
        {
            Title = title,
            Slug = await UniqueSlugAsync(req.Slug, title, null, ct),
            Excerpt = Clean(req.Excerpt, 500),
            BodyHtml = req.BodyHtml ?? "",
            CoverImageUrl = Clean(req.CoverImageUrl, 500),
            AuthorName = Clean(req.AuthorName, 120),
            MetaTitle = Clean(req.MetaTitle, 255),
            MetaDescription = Clean(req.MetaDescription, 500),
            Status = "Draft",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
        };
        db.Articles.Add(a);
        await db.SaveChangesAsync(ct);
        return Map(a);
    }

    public async Task<ArticleDto> UpdateAsync(long id, SaveArticleRequest req, CancellationToken ct = default)
    {
        var a = await Find(id, ct);
        a.Title = Require(req.Title, "Title");
        a.Slug = await UniqueSlugAsync(req.Slug, a.Title, id, ct);
        a.Excerpt = Clean(req.Excerpt, 500);
        a.BodyHtml = req.BodyHtml ?? "";
        a.CoverImageUrl = Clean(req.CoverImageUrl, 500);
        a.AuthorName = Clean(req.AuthorName, 120);
        a.MetaTitle = Clean(req.MetaTitle, 255);
        a.MetaDescription = Clean(req.MetaDescription, 500);
        a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(a);
    }

    public async Task<ArticleDto> SetPublishedAsync(long id, bool published, CancellationToken ct = default)
    {
        var a = await Find(id, ct);
        a.Status = published ? "Published" : "Draft";
        a.PublishedAt = published ? (a.PublishedAt ?? DateTime.UtcNow) : null;
        a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(a);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var a = await Find(id, ct);
        db.Articles.Remove(a);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ArticleDraftDto> DraftAsync(ArticleDraftRequest req, CancellationToken ct = default)
    {
        var topic = Require(req.Topic, "Topic");
        var (brandFragment, kit) = await brandKit.PromptFragmentAsync(ct);
        var language = string.IsNullOrWhiteSpace(req.Language) ? kit.Language : req.Language!.Trim();
        var brief = Clean(req.Brief, 500);

        var system =
            "You are a content-marketing writer for a small Indian online store. Write a genuinely useful, " +
            "reader-first blog article — not a sales pitch. Be specific and factual; never invent products, " +
            "prices or claims. Return ONLY compact JSON with keys: title, excerpt, bodyHtml, metaTitle, " +
            "metaDescription. bodyHtml is the article body as simple HTML using only <h2>, <h3>, <p>, <ul>, " +
            "<li>, <strong> and <em> tags (no <html>/<head>/<body>, no styles, no scripts). Aim for 600-900 " +
            "words. excerpt is 1-2 sentences. metaTitle <= 60 chars, metaDescription <= 155 chars.\n\n" + brandFragment;

        var user =
            $"Topic: {topic}\n" +
            (string.IsNullOrWhiteSpace(brief) ? "" : $"Extra guidance from the merchant: {brief}\n") +
            $"Write the article in {language}.";

        return await credits.MeterAsync(AiCreditPricing.GrowthArticle, async ai =>
        {
            var c = await ai.CompleteAsync(new AiPrompt(system, user, Json: true, MaxTokens: 2000), ct);
            var dto = Parse(c.Text, topic);
            return (dto, c);
        }, ct);
    }

    public async Task<PagedResult<ArticleSummaryDto>> PublicListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var q = db.Articles.AsNoTracking().Where(a => a.Status == "Published").OrderByDescending(a => a.PublishedAt);
        var total = await q.LongCountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new ArticleSummaryDto(a.ArticleId, a.Title, a.Slug, a.Excerpt, a.CoverImageUrl, a.AuthorName, a.Status, a.PublishedAt))
            .ToListAsync(ct);
        return new PagedResult<ArticleSummaryDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<ArticleDto?> PublicGetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var a = await db.Articles.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug && x.Status == "Published", ct);
        return a is null ? null : Map(a);
    }

    // ---- helpers ----

    private async Task<Article> Find(long id, CancellationToken ct) =>
        await db.Articles.FirstOrDefaultAsync(a => a.ArticleId == id, ct)
        ?? throw new AppException("Article not found.", StatusCodes.Status404NotFound);

    private async Task<string> UniqueSlugAsync(string? desired, string title, long? excludeId, CancellationToken ct)
    {
        var baseSlug = Slug.From(string.IsNullOrWhiteSpace(desired) ? title : desired!);
        var slug = baseSlug;
        var n = 2;
        while (await db.Articles.AnyAsync(a => a.Slug == slug && a.ArticleId != (excludeId ?? 0), ct))
            slug = $"{baseSlug}-{n++}";
        return slug;
    }

    private static ArticleDraftDto Parse(string json, string topic)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            string S(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            var title = S("title"); var body = S("bodyHtml");
            if (string.IsNullOrWhiteSpace(body)) throw new FormatException("empty body");
            return new ArticleDraftDto(
                string.IsNullOrWhiteSpace(title) ? topic : title,
                S("excerpt"), body,
                string.IsNullOrWhiteSpace(S("metaTitle")) ? title : S("metaTitle"),
                S("metaDescription"));
        }
        catch
        {
            throw new AppException("The AI draft came back malformed — please try again.", StatusCodes.Status502BadGateway);
        }
    }

    private static ArticleDto Map(Article a) =>
        new(a.ArticleId, a.Title, a.Slug, a.Excerpt, a.BodyHtml, a.CoverImageUrl, a.AuthorName,
            a.MetaTitle, a.MetaDescription, a.Status, a.PublishedAt, a.CreatedAt, a.UpdatedAt);

    private static string Require(string? v, string field) =>
        string.IsNullOrWhiteSpace(v) ? throw new AppException($"{field} is required.", StatusCodes.Status400BadRequest) : v.Trim();

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
