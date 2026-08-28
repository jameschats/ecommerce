using System.Text.Json;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Features.Ai;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Growth;

public sealed record KeywordIdeaDto(string Keyword, string Intent, string? Note);
public sealed record KeywordIdeasRequest(string? Seed, string? Language);

public sealed record ContentBriefRequest(string Keyword, string? Language);
public sealed record ContentBriefDto(string TargetKeyword, string SuggestedTitle, string MetaDescription,
    IReadOnlyList<string> Outline, IReadOnlyList<string> Questions);

public interface IGrowthSeoService
{
    /// <summary>SEO keyword/topic ideas for the store (seeded by a phrase, or auto from the catalog).</summary>
    Task<IReadOnlyList<KeywordIdeaDto>> KeywordIdeasAsync(KeywordIdeasRequest req, CancellationToken ct = default);
    /// <summary>A content brief for a target keyword — outline + questions to answer — that feeds the article writer.</summary>
    Task<ContentBriefDto> ContentBriefAsync(ContentBriefRequest req, CancellationToken ct = default);
}

/// <summary>
/// SEO assistant (v4 Phase-4 Track A) — the two genuinely-new IAiService consumers the roadmap names:
/// bulk keyword/topic ideas and content briefs. Both reuse the shipped credit-metering pattern; a brief
/// is designed to hand straight to the blog writer (G5).
/// </summary>
public sealed class GrowthSeoService(EcommerceDbContext db, IAiCreditService credits, IBrandKitService brandKit) : IGrowthSeoService
{
    public async Task<IReadOnlyList<KeywordIdeaDto>> KeywordIdeasAsync(KeywordIdeasRequest req, CancellationToken ct = default)
    {
        var (_, kit) = await brandKit.PromptFragmentAsync(ct);
        var language = string.IsNullOrWhiteSpace(req.Language) ? kit.Language : req.Language!.Trim();

        // Ground in the store's real categories so ideas are relevant even with no seed.
        var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive)
            .OrderByDescending(c => c.CategoryId).Select(c => c.Name).Take(15).ToListAsync(ct);
        var seed = string.IsNullOrWhiteSpace(req.Seed) ? "" : req.Seed!.Trim();

        var system =
            "You are an SEO strategist for a small Indian online store. Suggest realistic search keywords/topics " +
            "a shopper would actually type. Prefer specific long-tail phrases over generic head terms. Return ONLY " +
            "compact JSON: an array of up to 15 objects with keys keyword, intent (one of: informational, " +
            "commercial, transactional), note (a short reason or content angle, optional).";
        var user =
            (categories.Count > 0 ? $"The store sells in these categories: {string.Join(", ", categories)}.\n" : "") +
            (seed.Length > 0 ? $"Focus around: {seed}.\n" : "") +
            $"Give the keywords in {language}.";

        return await credits.MeterAsync(AiCreditPricing.GrowthKeywords, async ai =>
        {
            var c = await ai.CompleteAsync(new AiPrompt(system, user, Json: true, MaxTokens: 800), ct);
            return (ParseKeywords(c.Text), c);
        }, ct);
    }

    public async Task<ContentBriefDto> ContentBriefAsync(ContentBriefRequest req, CancellationToken ct = default)
    {
        var keyword = string.IsNullOrWhiteSpace(req.Keyword)
            ? throw new AppException("Enter a target keyword.", StatusCodes.Status400BadRequest)
            : req.Keyword.Trim();
        var (_, kit) = await brandKit.PromptFragmentAsync(ct);
        var language = string.IsNullOrWhiteSpace(req.Language) ? kit.Language : req.Language!.Trim();

        var system =
            "You are an SEO content strategist. For the given target keyword, produce a brief for a blog article. " +
            "Return ONLY compact JSON with keys: targetKeyword, suggestedTitle (<=60 chars), metaDescription " +
            "(<=155 chars), outline (array of 4-7 H2 section headings), questions (array of 3-6 real questions " +
            "the article should answer). Reader-first, not salesy.";
        var user = $"Target keyword: {keyword}\nWrite the brief in {language}.";

        return await credits.MeterAsync(AiCreditPricing.GrowthBrief, async ai =>
        {
            var c = await ai.CompleteAsync(new AiPrompt(system, user, Json: true, MaxTokens: 700), ct);
            return (ParseBrief(c.Text, keyword), c);
        }, ct);
    }

    private static IReadOnlyList<KeywordIdeaDto> ParseKeywords(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement
                : doc.RootElement.TryGetProperty("keywords", out var k) ? k : default;
            var list = new List<KeywordIdeaDto>();
            if (arr.ValueKind == JsonValueKind.Array)
                foreach (var e in arr.EnumerateArray())
                {
                    var kw = Str(e, "keyword");
                    if (string.IsNullOrWhiteSpace(kw)) continue;
                    list.Add(new KeywordIdeaDto(kw, Str(e, "intent") is { Length: > 0 } i ? i : "informational", Str(e, "note") is { Length: > 0 } n ? n : null));
                }
            if (list.Count == 0) throw new FormatException();
            return list;
        }
        catch { throw new AppException("The AI response came back malformed — please try again.", StatusCodes.Status502BadGateway); }
    }

    private static ContentBriefDto ParseBrief(string json, string keyword)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            return new ContentBriefDto(
                Str(r, "targetKeyword") is { Length: > 0 } tk ? tk : keyword,
                Str(r, "suggestedTitle"), Str(r, "metaDescription"),
                StrArray(r, "outline"), StrArray(r, "questions"));
        }
        catch { throw new AppException("The AI response came back malformed — please try again.", StatusCodes.Status502BadGateway); }
    }

    private static string Str(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static IReadOnlyList<string> StrArray(JsonElement e, string key)
    {
        var outp = new List<string>();
        if (e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array)
            foreach (var item in v.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s) outp.Add(s);
        return outp;
    }
}
