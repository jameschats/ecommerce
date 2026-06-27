namespace ecomm.api.Common.Models;

/// <summary>
/// A page of results plus the paging metadata the client needs to render
/// pagination controls. Used by list endpoints (catalog, orders, etc.).
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
