using System.Text;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Growth;

public sealed record BrandKitDto(
    string Tone, string Language, string? Audience, bool UseEmoji, string? Hashtags, string? DoNotSay);

public interface IBrandKitService
{
    /// <summary>The tenant's brand kit, or sensible defaults if they haven't set one yet.</summary>
    Task<BrandKitDto> GetAsync(CancellationToken ct = default);
    Task<BrandKitDto> SaveAsync(BrandKitDto req, CancellationToken ct = default);

    /// <summary>
    /// The brand voice as a prompt fragment for the generation service. Returns the fetched kit too
    /// so a caller that also needs the language doesn't have to load it twice.
    /// </summary>
    Task<(string promptFragment, BrandKitDto kit)> PromptFragmentAsync(CancellationToken ct = default);
}

/// <summary>
/// Per-tenant brand voice (G1). Kept deliberately small: the value is that every generation prompt
/// carries the same tone, language and guardrails, so the output stops sounding generic.
/// </summary>
public sealed class BrandKitService(EcommerceDbContext db) : IBrandKitService
{
    private static readonly HashSet<string> Tones = new(StringComparer.OrdinalIgnoreCase) { "friendly", "premium", "value", "playful" };
    private static readonly HashSet<string> Languages = new(StringComparer.OrdinalIgnoreCase) { "English", "Hindi", "Tamil", "Telugu", "Hinglish" };

    public async Task<BrandKitDto> GetAsync(CancellationToken ct = default)
    {
        var kit = await db.GrowthBrandKits.AsNoTracking().FirstOrDefaultAsync(ct);
        return kit is null
            ? new BrandKitDto("friendly", "English", null, true, null, null)
            : Map(kit);
    }

    public async Task<BrandKitDto> SaveAsync(BrandKitDto req, CancellationToken ct = default)
    {
        var kit = await db.GrowthBrandKits.FirstOrDefaultAsync(ct);
        var now = DateTime.UtcNow;
        if (kit is null)
        {
            kit = new GrowthBrandKit { CreatedAt = now };
            db.GrowthBrandKits.Add(kit);
        }
        else
        {
            kit.UpdatedAt = now;
        }

        kit.Tone = Tones.Contains(req.Tone ?? "") ? req.Tone!.ToLowerInvariant() : "friendly";
        kit.Language = Languages.Contains(req.Language ?? "") ? Normalize(req.Language!) : "English";
        kit.Audience = Clean(req.Audience, 300);
        kit.UseEmoji = req.UseEmoji;
        kit.Hashtags = Clean(req.Hashtags, 500);
        kit.DoNotSay = Clean(req.DoNotSay, 500);

        await db.SaveChangesAsync(ct);
        return Map(kit);
    }

    public async Task<(string, BrandKitDto)> PromptFragmentAsync(CancellationToken ct = default)
    {
        var kit = await GetAsync(ct);
        var sb = new StringBuilder();

        sb.Append("BRAND VOICE — follow this exactly:\n");
        sb.Append($"- Tone: {kit.Tone}.\n");
        sb.Append($"- Write in: {kit.Language}");
        if (kit.Language.Equals("Hinglish", StringComparison.OrdinalIgnoreCase))
            sb.Append(" (natural Hindi-English mix in Roman script, the way Indian shoppers actually message)");
        sb.Append(".\n");
        if (!string.IsNullOrWhiteSpace(kit.Audience))
            sb.Append($"- Audience: {kit.Audience}.\n");
        sb.Append(kit.UseEmoji ? "- Emoji: a few, tastefully.\n" : "- Emoji: none.\n");
        if (!string.IsNullOrWhiteSpace(kit.Hashtags))
            sb.Append($"- Reuse these hashtags where a post calls for them: {kit.Hashtags}.\n");
        if (!string.IsNullOrWhiteSpace(kit.DoNotSay))
            sb.Append($"- Never use these words or make these claims: {kit.DoNotSay}.\n");

        return (sb.ToString(), kit);
    }

    private static BrandKitDto Map(GrowthBrandKit k) =>
        new(k.Tone, k.Language, k.Audience, k.UseEmoji, k.Hashtags, k.DoNotSay);

    private static string Normalize(string language) =>
        char.ToUpperInvariant(language[0]) + language[1..].ToLowerInvariant();

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
