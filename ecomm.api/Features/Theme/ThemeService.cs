using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Theme;

public sealed record ThemeDto(long ThemeId, string Name, Dictionary<string, string> Settings);
public sealed record UpdateThemeRequest(Dictionary<string, string> Settings);

public interface IThemeService
{
    Task<ThemeDto> GetActiveAsync(CancellationToken ct = default);
    Task<ThemeDto> UpdateAsync(Dictionary<string, string> settings, CancellationToken ct = default);
}

public sealed class ThemeService : IThemeService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;

    public ThemeService(EcommerceDbContext db) => _db = db;

    public async Task<ThemeDto> GetActiveAsync(CancellationToken ct = default)
    {
        var theme = await ActiveThemeQuery().Include(t => t.Settings).FirstOrDefaultAsync(ct);
        if (theme is null) return new ThemeDto(0, "Default", new());

        var map = theme.Settings.ToDictionary(s => s.SettingKey, s => s.SettingValue ?? string.Empty);
        return new ThemeDto(theme.ThemeId, theme.Name, map);
    }

    public async Task<ThemeDto> UpdateAsync(Dictionary<string, string> settings, CancellationToken ct = default)
    {
        var theme = await ActiveThemeQuery().Include(t => t.Settings).FirstOrDefaultAsync(ct)
            ?? throw new Common.Exceptions.AppException("No active theme found.");

        var now = DateTime.UtcNow;
        foreach (var (key, value) in settings)
        {
            var existing = theme.Settings.FirstOrDefault(s => s.SettingKey == key);
            if (existing is null)
                theme.Settings.Add(new ThemeSetting { ThemeId = theme.ThemeId, SettingKey = key, SettingValue = value, CreatedAt = now });
            else
            {
                existing.SettingValue = value;
                existing.UpdatedAt = now;
            }
        }
        theme.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return await GetActiveAsync(ct);
    }

    private IQueryable<Data.Entities.Theme> ActiveThemeQuery() =>
        _db.Themes.Where(t => t.TenantId == Tenant).OrderByDescending(t => t.IsActive).ThenBy(t => t.ThemeId);
}
