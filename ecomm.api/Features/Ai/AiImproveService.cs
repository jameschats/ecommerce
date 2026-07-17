using System.Text.Json;
using ecomm.api.Common.Exceptions;

namespace ecomm.api.Features.Ai;

public sealed record SeoResult(string Title, string MetaDescription);

/// <summary>
/// The "✨ Improve with AI" text helpers (AI-1). Each call runs through <see cref="IAiCreditService.MeterAsync"/>
/// so credits are checked, the usage is logged, and the debit only happens on success. Prompts are
/// constrained to rewrite/expand the merchant's own copy without inventing facts.
/// </summary>
public interface IAiImproveService
{
    Task<string> ImproveAsync(string purpose, string text, string? context, CancellationToken ct = default);
    Task<SeoResult> SeoAsync(string name, string? description, CancellationToken ct = default);
}

public sealed class AiImproveService(IAiCreditService credits) : IAiImproveService
{
    public async Task<string> ImproveAsync(string purpose, string text, string? context, CancellationToken ct = default)
    {
        var (system, feature, maxTokens) = PromptFor(purpose);
        var trimmed = (text ?? string.Empty).Trim();
        var ctx = string.IsNullOrWhiteSpace(context) ? string.Empty : $"Context: {context.Trim()}\n\n";
        var user = trimmed.Length == 0
            ? $"{ctx}Write it from scratch."
            : $"{ctx}Improve this:\n\"\"\"\n{trimmed}\n\"\"\"";

        return await credits.MeterAsync(feature, async ai =>
        {
            var c = await ai.CompleteAsync(new AiPrompt(system, user, Json: false, MaxTokens: maxTokens), ct);
            return (Clean(c.Text), c);
        }, ct);
    }

    public async Task<SeoResult> SeoAsync(string name, string? description, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new AppException("Add a product name first.", 400);

        const string system =
            "You write SEO metadata for products on an Indian e-commerce store. " +
            "Return STRICT JSON with exactly two string fields: {\"title\": ..., \"metaDescription\": ...}. " +
            "title must be at most 60 characters; metaDescription at most 155 characters. " +
            "Be specific and factual — never invent specifications or claims not provided.";
        var user = $"Product: {name.Trim()}"
                 + (string.IsNullOrWhiteSpace(description) ? string.Empty : $"\nDescription: {description!.Trim()}");

        return await credits.MeterAsync(AiCreditPricing.Seo, async ai =>
        {
            var c = await ai.CompleteAsync(new AiPrompt(system, user, Json: true, MaxTokens: 300), ct);
            return (ParseSeo(c.Text, name.Trim()), c);
        }, ct);
    }

    // purpose → (system prompt, credit feature, max tokens)
    private static (string System, string Feature, int MaxTokens) PromptFor(string purpose) => purpose switch
    {
        "product-description"  => (Base("product description", "2-4 short sentences highlighting benefits and key features"), AiCreditPricing.ImproveText, 400),
        "short-description"    => (Base("one-line product summary", "a single punchy sentence of at most ~15 words"), AiCreditPricing.ImproveText, 120),
        "category-description" => (Base("category description", "1-2 inviting sentences describing the kind of products in this category"), AiCreditPricing.Category, 200),
        "page"                 => (Base("page copy", "clear, well-structured storefront page copy"), AiCreditPricing.ImproveText, 600),
        _                      => (Base("text", "clear and concise copy"), AiCreditPricing.ImproveText, 400),
    };

    private static string Base(string thing, string shape) =>
        $"You are an expert e-commerce copywriter for an Indian online store. Write or rewrite the {thing} so it is {shape}. " +
        "Keep it factual — never invent specifications, sizes, materials, prices, or claims that are not given. " +
        $"Write in clear, natural English. Return ONLY the {thing} text: no preamble, no markdown, no surrounding quotes, no labels.";

    private static string Clean(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && s[0] is '"' or '“' && s[^1] is '"' or '”') s = s[1..^1].Trim();
        return s;
    }

    private static SeoResult ParseSeo(string json, string fallbackName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            var title = r.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            var meta = r.TryGetProperty("metaDescription", out var m) ? m.GetString() ?? string.Empty : string.Empty;
            return new SeoResult(
                Trunc(string.IsNullOrWhiteSpace(title) ? fallbackName : title, 70),
                Trunc(meta, 170));
        }
        catch (JsonException)
        {
            throw new AppException("The AI returned an unexpected response. Please try again.", 502);
        }
    }

    private static string Trunc(string s, int max) { s = s.Trim(); return s.Length <= max ? s : s[..max].Trim(); }
}
