using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>
/// Reads the tenant's published-theme colours to seed a first-time brand kit. This is the single
/// place the Marketing Studio touches the theme tables — everything else goes through
/// <see cref="IBrandThemeDefaults"/>, so on extraction only this adapter changes (to an HTTP call).
/// </summary>
public sealed class BrandThemeDefaults(EcommerceDbContext db) : IBrandThemeDefaults
{
    public async Task<(string Primary, string Secondary, string Accent)?> TryGetAsync(CancellationToken ct = default)
    {
        // Theme is ITenantScoped → auto-filtered to the current tenant.
        var theme = await db.Themes.AsNoTracking()
            .Where(t => t.Status == "Published")
            .OrderByDescending(t => t.ThemeId)
            .FirstOrDefaultAsync(ct);
        if (theme is null) return null;

        var settings = await db.ThemeSettings.AsNoTracking()
            .Where(s => s.ThemeId == theme.ThemeId)
            .ToListAsync(ct);
        if (settings.Count == 0) return null;

        string? Val(string key) => settings
            .FirstOrDefault(s => string.Equals(s.SettingKey, key, StringComparison.OrdinalIgnoreCase))?.SettingValue;

        var primary = Val("PrimaryColor");
        var secondary = Val("SecondaryColor");
        var accent = Val("AccentColor");
        if (string.IsNullOrWhiteSpace(primary)) return null;   // no usable palette

        return (primary!, secondary ?? primary!, accent ?? primary!);
    }
}
