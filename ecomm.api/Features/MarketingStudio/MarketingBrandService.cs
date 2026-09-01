using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>The visual brand kit as seen/edited by the merchant. All fields optional bar the colours.</summary>
public sealed record MarketingBrandDto(
    string? CompanyName, string? Tagline, string? LogoUrl,
    string PrimaryColor, string SecondaryColor, string AccentColor, string? Font,
    bool IncludeLogoByDefault, bool IncludeNameByDefault,
    string? InstagramHandle, string? FacebookHandle, string? LinkedInHandle,
    string? PinterestHandle, string? YouTubeHandle, string? WhatsAppNumber, string? WebsiteUrl);

/// <summary>
/// Port the Marketing Studio uses to seed brand colours from the store's active theme, so a
/// merchant's first brand kit already matches their storefront. Kept as an interface (not a direct
/// theme dependency) so the module stays extractable — on a split this becomes an HTTP call. See
/// marketing-studio-plan.md §3.10.
/// </summary>
public interface IBrandThemeDefaults
{
    Task<(string Primary, string Secondary, string Accent)?> TryGetAsync(CancellationToken ct = default);
}

public interface IMarketingBrandService
{
    /// <summary>The tenant's brand kit, or theme-seeded defaults if they haven't saved one yet.</summary>
    Task<MarketingBrandDto> GetAsync(CancellationToken ct = default);
    Task<MarketingBrandDto> SaveAsync(MarketingBrandDto req, CancellationToken ct = default);
}

/// <summary>
/// Per-tenant visual brand kit (MS0). Deliberately small: its value is that every generated
/// creative starts from the same logo, name, colours and toggles. Part of the Marketing Studio
/// module; depends on commerce only through <see cref="IBrandThemeDefaults"/> (a thin port).
/// </summary>
public sealed class MarketingBrandService(EcommerceDbContext db, IBrandThemeDefaults themeDefaults)
    : IMarketingBrandService
{
    public async Task<MarketingBrandDto> GetAsync(CancellationToken ct = default)
    {
        var p = await db.MarketingBrandProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
        if (p is not null) return Map(p);

        // No kit yet — offer theme colours as the starting point, falling back to neutrals.
        var theme = await themeDefaults.TryGetAsync(ct);
        return new MarketingBrandDto(
            null, null, null,
            theme?.Primary ?? "#111827", theme?.Secondary ?? "#6b7280", theme?.Accent ?? "#2563eb",
            null, true, true, null, null, null, null, null, null, null);
    }

    public async Task<MarketingBrandDto> SaveAsync(MarketingBrandDto req, CancellationToken ct = default)
    {
        var p = await db.MarketingBrandProfiles.FirstOrDefaultAsync(ct);
        var now = DateTime.UtcNow;
        if (p is null)
        {
            p = new MarketingBrandProfile { CreatedAt = now };
            db.MarketingBrandProfiles.Add(p);
        }
        else
        {
            p.UpdatedAt = now;
        }

        p.CompanyName = Clean(req.CompanyName, 200);
        p.Tagline = Clean(req.Tagline, 300);
        p.LogoUrl = Clean(req.LogoUrl, 500);
        p.PrimaryColor = Hex(req.PrimaryColor, "#111827");
        p.SecondaryColor = Hex(req.SecondaryColor, "#6b7280");
        p.AccentColor = Hex(req.AccentColor, "#2563eb");
        p.Font = Clean(req.Font, 80);
        p.IncludeLogoByDefault = req.IncludeLogoByDefault;
        p.IncludeNameByDefault = req.IncludeNameByDefault;
        p.InstagramHandle = Clean(req.InstagramHandle, 120);
        p.FacebookHandle = Clean(req.FacebookHandle, 120);
        p.LinkedInHandle = Clean(req.LinkedInHandle, 120);
        p.PinterestHandle = Clean(req.PinterestHandle, 120);
        p.YouTubeHandle = Clean(req.YouTubeHandle, 120);
        p.WhatsAppNumber = Clean(req.WhatsAppNumber, 30);
        p.WebsiteUrl = Clean(req.WebsiteUrl, 300);

        await db.SaveChangesAsync(ct);
        return Map(p);
    }

    private static MarketingBrandDto Map(MarketingBrandProfile p) => new(
        p.CompanyName, p.Tagline, p.LogoUrl,
        p.PrimaryColor, p.SecondaryColor, p.AccentColor, p.Font,
        p.IncludeLogoByDefault, p.IncludeNameByDefault,
        p.InstagramHandle, p.FacebookHandle, p.LinkedInHandle,
        p.PinterestHandle, p.YouTubeHandle, p.WhatsAppNumber, p.WebsiteUrl);

    /// <summary>Accept a #rgb / #rrggbb hex colour, else fall back to the default.</summary>
    private static string Hex(string? v, string fallback)
    {
        if (string.IsNullOrWhiteSpace(v)) return fallback;
        var t = v.Trim();
        if (t[0] != '#' || (t.Length != 4 && t.Length != 7)) return fallback;
        for (var i = 1; i < t.Length; i++)
            if (!Uri.IsHexDigit(t[i])) return fallback;
        return t.ToLowerInvariant();
    }

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
