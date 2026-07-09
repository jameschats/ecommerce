using System.Text.Json;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Navigation;

public sealed record MenuItemDto(string Label, string Url, List<MenuItemDto>? Children = null);
public sealed record MenuDto(string Handle, string Title, IReadOnlyList<MenuItemDto> Items);
public sealed record SaveMenuRequest(IReadOnlyList<MenuItemDto> Items);

public sealed record RedirectDto(long UrlRedirectId, string FromPath, string ToPath);
public sealed record SaveRedirectRequest(string FromPath, string ToPath);

public interface INavigationService
{
    Task<IReadOnlyList<MenuDto>> ListMenusAsync(CancellationToken ct = default);
    Task<MenuDto> GetMenuAsync(string handle, CancellationToken ct = default);
    Task<MenuDto> SaveMenuAsync(string handle, SaveMenuRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<RedirectDto>> ListRedirectsAsync(CancellationToken ct = default);
    Task<RedirectDto> CreateRedirectAsync(SaveRedirectRequest req, CancellationToken ct = default);
    Task<RedirectDto> UpdateRedirectAsync(long id, SaveRedirectRequest req, CancellationToken ct = default);
    Task DeleteRedirectAsync(long id, CancellationToken ct = default);
    Task<string?> ResolveRedirectAsync(string fromPath, CancellationToken ct = default);
}

/// <summary>Storefront navigation menus (main/footer/account) + URL redirects. Tenant-scoped by global filters.</summary>
public sealed class NavigationService(EcommerceDbContext db) : INavigationService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    /// <summary>The known menus, seeded on first read.</summary>
    private static readonly (string Handle, string Title)[] Known =
    {
        ("main-menu", "Main menu"), ("footer", "Footer menu"), ("account", "Customer account menu"),
    };

    // ---- menus ----
    public async Task<IReadOnlyList<MenuDto>> ListMenusAsync(CancellationToken ct = default)
    {
        var result = new List<MenuDto>();
        foreach (var (handle, _) in Known) result.Add(await GetMenuAsync(handle, ct));
        return result;
    }

    public async Task<MenuDto> GetMenuAsync(string handle, CancellationToken ct = default)
    {
        handle = handle.Trim().ToLowerInvariant();
        var known = Known.FirstOrDefault(k => k.Handle == handle);
        if (known.Handle is null) throw new AppException("Unknown menu.", StatusCodes.Status404NotFound);
        var menu = await db.Menus.FirstOrDefaultAsync(m => m.Handle == handle, ct);
        return new MenuDto(handle, known.Title, ParseItems(menu?.ItemsJson));
    }

    public async Task<MenuDto> SaveMenuAsync(string handle, SaveMenuRequest req, CancellationToken ct = default)
    {
        handle = handle.Trim().ToLowerInvariant();
        var known = Known.FirstOrDefault(k => k.Handle == handle);
        if (known.Handle is null) throw new AppException("Unknown menu.", StatusCodes.Status404NotFound);

        var menu = await db.Menus.FirstOrDefaultAsync(m => m.Handle == handle, ct);
        if (menu is null)
        {
            menu = new Menu { Handle = handle, Title = known.Title, CreatedAt = DateTime.UtcNow };
            db.Menus.Add(menu);
        }
        var items = (req.Items ?? []).Where(i => !string.IsNullOrWhiteSpace(i.Label)).ToList();
        menu.ItemsJson = JsonSerializer.Serialize(items, Json);
        menu.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new MenuDto(handle, known.Title, items);
    }

    // ---- redirects ----
    public async Task<IReadOnlyList<RedirectDto>> ListRedirectsAsync(CancellationToken ct = default) =>
        await db.UrlRedirects.OrderBy(r => r.FromPath)
            .Select(r => new RedirectDto(r.UrlRedirectId, r.FromPath, r.ToPath)).ToListAsync(ct);

    public async Task<RedirectDto> CreateRedirectAsync(SaveRedirectRequest req, CancellationToken ct = default)
    {
        var (from, to) = Normalize(req);
        if (await db.UrlRedirects.AnyAsync(r => r.FromPath == from, ct))
            throw new AppException($"A redirect from '{from}' already exists.", StatusCodes.Status409Conflict);
        var r = new UrlRedirect { FromPath = from, ToPath = to, CreatedAt = DateTime.UtcNow };
        db.UrlRedirects.Add(r);
        await db.SaveChangesAsync(ct);
        return new RedirectDto(r.UrlRedirectId, r.FromPath, r.ToPath);
    }

    public async Task<RedirectDto> UpdateRedirectAsync(long id, SaveRedirectRequest req, CancellationToken ct = default)
    {
        var r = await db.UrlRedirects.FirstOrDefaultAsync(x => x.UrlRedirectId == id, ct) ?? throw NotFound();
        var (from, to) = Normalize(req);
        if (from != r.FromPath && await db.UrlRedirects.AnyAsync(x => x.FromPath == from && x.UrlRedirectId != id, ct))
            throw new AppException($"A redirect from '{from}' already exists.", StatusCodes.Status409Conflict);
        r.FromPath = from; r.ToPath = to; r.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new RedirectDto(r.UrlRedirectId, r.FromPath, r.ToPath);
    }

    public async Task DeleteRedirectAsync(long id, CancellationToken ct = default)
    {
        var r = await db.UrlRedirects.FirstOrDefaultAsync(x => x.UrlRedirectId == id, ct) ?? throw NotFound();
        db.UrlRedirects.Remove(r);
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> ResolveRedirectAsync(string fromPath, CancellationToken ct = default)
    {
        var from = NormalizePath(fromPath);
        return await db.UrlRedirects.Where(r => r.FromPath == from).Select(r => r.ToPath).FirstOrDefaultAsync(ct);
    }

    // ---- helpers ----
    private static List<MenuItemDto> ParseItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<MenuItemDto>>(json, Json) ?? []; }
        catch { return []; }
    }

    private static (string from, string to) Normalize(SaveRedirectRequest req)
    {
        var from = NormalizePath(req.FromPath);
        var to = req.ToPath?.Trim() ?? "";
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) throw new AppException("Both a from and to path are required.");
        if (from == NormalizePath(to)) throw new AppException("A redirect can't point to itself.");
        return (from, to.StartsWith("http") ? to : NormalizePath(to));
    }

    private static string NormalizePath(string? path)
    {
        var p = (path ?? "").Trim();
        if (p.Length == 0) return "";
        if (p.StartsWith("http")) return p;
        if (!p.StartsWith('/')) p = "/" + p;
        return p.TrimEnd('/') is "" ? "/" : p.TrimEnd('/');
    }

    private static AppException NotFound() => new("Redirect not found.", StatusCodes.Status404NotFound);
}
