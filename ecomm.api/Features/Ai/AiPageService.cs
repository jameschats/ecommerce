using System.Text;
using System.Text.Json.Nodes;
using ecomm.api.Common.Exceptions;
using ecomm.api.Features.Cms;
using ecomm.api.Features.Cms.SectionTypes;

namespace ecomm.api.Features.Ai;

public sealed record GeneratePageRequest(string Title, string Prompt);
public sealed record GeneratedPageDto(long PageId, string Slug, int Sections);

public interface IAiPageService
{
    Task<GeneratedPageDto> GenerateAsync(GeneratePageRequest req, CancellationToken ct = default);
}

/// <summary>
/// AI-5 page creation. The merchant describes a page (About/Contact/FAQ/landing…); the AI returns an ordered
/// list of page-builder sections which are created as a NEW draft page via <see cref="ICmsService"/> (so all
/// the existing validation + HTML sanitization is reused). The merchant lands in the builder to edit/publish —
/// nothing goes live automatically.
/// </summary>
public sealed class AiPageService(IAiCreditService credits, ICmsService cms) : IAiPageService
{
    // Static, any-page content sections suitable for a written page (no dynamic/group/product sections).
    private static readonly string[] Allowed = { "RichText", "Hero", "ImageWithText", "Multicolumn", "Testimonials", "CtaNewsletter" };

    public async Task<GeneratedPageDto> GenerateAsync(GeneratePageRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) throw new AppException("Give the page a title.", 400);
        if (string.IsNullOrWhiteSpace(req.Prompt)) throw new AppException("Describe what the page should contain.", 400);

        var sections = await credits.MeterAsync(AiCreditPricing.Page, async ai =>
        {
            var user = $"Page title: {req.Title.Trim()}\nWhat it should contain: {req.Prompt.Trim()}";
            var c = await ai.CompleteAsync(new AiPrompt(SystemPrompt(), user, Json: true, MaxTokens: 2500), ct);
            return (ParseSections(c.Text), c);
        }, ct);

        if (sections.Count == 0) throw new AppException("The AI didn't return any usable sections. Please try again.", 502);

        // Create a NEW draft page with a unique title/slug, then add each section (CMS sanitizes settings).
        var existing = new HashSet<string>((await cms.ListPagesAsync(ct)).Select(p => p.Slug), StringComparer.OrdinalIgnoreCase);
        var baseTitle = req.Title.Trim();
        var title = baseTitle;
        var n = 2;
        while (existing.Contains(PageSlug(title))) title = $"{baseTitle} {n++}";

        var page = await cms.CreatePageAsync(new SavePageRequest(title, title, IsPublished: false, null, null), ct);
        var count = 0;
        foreach (var s in sections)
        {
            var added = await cms.AddSectionAsync(new AddSectionRequest(page.PageId, s.Type), ct);
            await cms.UpdateSectionAsync(added.PageSectionId,
                new SaveSectionRequest(s.Title, s.Settings, s.Blocks, IsVisible: true, null, null), ct);
            count++;
        }
        return new GeneratedPageDto(page.PageId, page.Slug, count);
    }

    // ---- prompt ----
    private static string SystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You build a storefront content page as an ordered list of sections for a visual page builder.");
        sb.AppendLine("Return STRICT JSON: {\"sections\":[{\"type\":<type>,\"title\":<string>,\"settings\":{...},\"blocks\":[...]}]}.");
        sb.AppendLine("Allowed section types and their fields:");
        foreach (var key in Allowed)
        {
            var schema = SectionTypeRegistry.Get(key)!;
            var settings = string.Join(", ", schema.Settings.Select(f => $"{f.Key}({FieldHint(f)})"));
            sb.Append($"- {key}: settings {{ {settings} }}");
            if (schema.BlockTypes.Count > 0)
            {
                var bt = schema.BlockTypes[0];
                var bf = string.Join(", ", bt.Fields.Select(f => $"{f.Key}({FieldHint(f)})"));
                sb.Append($"; blocks[] each {{ {bf} }} (max {schema.MaxBlocks?.ToString() ?? "6"})");
            }
            sb.AppendLine();
        }
        sb.AppendLine("Rules: use 3-6 sections. Put the main written content in RichText 'content' as simple HTML using only <h2>, <h3>, <p>, <ul>, <li>, <strong>, <em>. " +
                      "Leave every image field as an empty string — the merchant adds photos later. Use \"#\" for any link you don't know. " +
                      "Stay factual and on-topic for the described page. Output ONLY the JSON.");
        return sb.ToString();
    }

    private static string FieldHint(FieldSchema f) =>
        f.Type == "select" ? "one of: " + string.Join("/", f.Options ?? Array.Empty<string>()) : f.Type;

    // ---- parsing ----
    private sealed record GenSection(string Type, string? Title, string Settings, string Blocks);

    private static List<GenSection> ParseSections(string json)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (System.Text.Json.JsonException) { throw new AppException("The AI returned an unexpected response. Please try again.", 502); }

        if (root?["sections"] is not JsonArray arr)
            throw new AppException("The AI didn't return any sections. Please try again.", 502);

        var list = new List<GenSection>();
        foreach (var node in arr)
        {
            if (node is not JsonObject o) continue;
            var type = AsString(o["type"])?.Trim();
            if (string.IsNullOrEmpty(type)) continue;
            var schema = SectionTypeRegistry.Get(type);
            if (schema is null || !Allowed.Contains(schema.Key, StringComparer.OrdinalIgnoreCase)) continue;

            var settings = (o["settings"] as JsonObject)?.ToJsonString() ?? "{}";
            var blocks = (o["blocks"] as JsonArray)?.ToJsonString() ?? "[]";
            list.Add(new GenSection(schema.Key, AsString(o["title"]), settings, blocks));
        }
        return list;
    }

    private static string? AsString(JsonNode? node)
    {
        try { return node?.GetValue<string>(); }
        catch { return node?.ToString(); }
    }

    // Mirrors CmsService.NormalizeSlug so the uniqueness pre-check matches what CreatePageAsync computes.
    private static string PageSlug(string title)
    {
        var s = System.Text.RegularExpressions.Regex.Replace(title.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(s) ? "page" : s;
    }
}
