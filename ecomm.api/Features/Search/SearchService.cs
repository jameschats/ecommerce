using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Search;

public sealed record PopularTermDto(string Term, long Count);

public interface ISearchService
{
    Task LogAsync(string query, int resultCount, long? userId, CancellationToken ct = default);
    Task<List<string>> SuggestAsync(string prefix, CancellationToken ct = default);
    Task<List<PopularTermDto>> PopularAsync(int top, CancellationToken ct = default);
}

public sealed class SearchService : ISearchService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;

    public SearchService(EcommerceDbContext db) => _db = db;

    public async Task LogAsync(string query, int resultCount, long? userId, CancellationToken ct = default)
    {
        var term = query.Trim().ToLowerInvariant();
        if (term.Length < 2) return;

        var now = DateTime.UtcNow;
        _db.SearchLogs.Add(new SearchLog { TenantId = Tenant, UserId = userId, QueryText = term, ResultsCount = resultCount, CreatedAt = now });

        var popular = await _db.PopularSearches.FirstOrDefaultAsync(p => p.TenantId == Tenant && p.Term == term, ct);
        if (popular is null)
            _db.PopularSearches.Add(new PopularSearch { TenantId = Tenant, Term = term, SearchCount = 1, LastSearchedAt = now });
        else
        {
            popular.SearchCount++;
            popular.LastSearchedAt = now;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<string>> SuggestAsync(string prefix, CancellationToken ct = default)
    {
        var s = (prefix ?? string.Empty).Trim();
        if (s.Length < 2) return new List<string>();

        // Products first (the primary intent), then matching category and brand names so a shopper can
        // jump straight to a section. Deduped, product suggestions kept ahead of taxonomy ones.
        var products = await _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted && p.IsActive && p.Status == "Active" && p.Name.Contains(s))
            .OrderBy(p => p.Name).Select(p => p.Name).Distinct().Take(6).ToListAsync(ct);

        var categories = await _db.Categories
            .Where(c => c.TenantId == Tenant && c.IsActive && c.Name.Contains(s))
            .OrderBy(c => c.Name).Select(c => c.Name).Take(3).ToListAsync(ct);

        var brands = await _db.Brands
            .Where(b => b.TenantId == Tenant && b.IsActive && b.Name.Contains(s))
            .OrderBy(b => b.Name).Select(b => b.Name).Take(3).ToListAsync(ct);

        return products
            .Concat(categories).Concat(brands)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10).ToList();
    }

    public Task<List<PopularTermDto>> PopularAsync(int top, CancellationToken ct = default) =>
        _db.PopularSearches
            .Where(p => p.TenantId == Tenant)
            .OrderByDescending(p => p.SearchCount)
            .Take(Math.Clamp(top, 1, 20))
            .Select(p => new PopularTermDto(p.Term, p.SearchCount))
            .ToListAsync(ct);
}
