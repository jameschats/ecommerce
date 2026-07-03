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

    public Task<List<string>> SuggestAsync(string prefix, CancellationToken ct = default)
    {
        var s = (prefix ?? string.Empty).Trim();
        if (s.Length < 2) return Task.FromResult(new List<string>());

        return _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted && p.IsActive && p.Status == "Active" && p.Name.Contains(s))
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .Distinct()
            .Take(8)
            .ToListAsync(ct);
    }

    public Task<List<PopularTermDto>> PopularAsync(int top, CancellationToken ct = default) =>
        _db.PopularSearches
            .Where(p => p.TenantId == Tenant)
            .OrderByDescending(p => p.SearchCount)
            .Take(Math.Clamp(top, 1, 20))
            .Select(p => new PopularTermDto(p.Term, p.SearchCount))
            .ToListAsync(ct);
}
