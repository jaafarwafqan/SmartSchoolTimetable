using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Common;

/// <summary>List parameters shared by every list endpoint: normalized Arabic search, sort, paging, archive filter.</summary>
public sealed record ListQuery(string? Search, string? Sort, int? Page, int? PageSize, bool? IncludeArchived)
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public int SafePage => Page is > 0 ? Page.Value : 1;
    public int SafePageSize => PageSize is > 0 ? Math.Min(PageSize.Value, MaxPageSize) : DefaultPageSize;
    public string NormalizedSearch => ArabicText.Normalize(Search);
    public bool WithArchived => IncludeArchived == true;

    /// <summary>Sort key without direction prefix; a leading "-" means descending.</summary>
    public (string Key, bool Descending) SortKey(string defaultKey)
    {
        var value = string.IsNullOrWhiteSpace(Sort) ? defaultKey : Sort.Trim();
        return value.StartsWith('-') ? (value[1..], true) : (value, false);
    }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public static class Paging
{
    public static async Task<PagedResult<TResult>> ToPageAsync<TEntity, TResult>(
        this IDataStore store,
        IQueryable<TEntity> query,
        ListQuery listQuery,
        Func<TEntity, TResult> map,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(listQuery);
        ArgumentNullException.ThrowIfNull(map);
        var total = await store.CountAsync(query, cancellationToken);
        var page = listQuery.SafePage;
        var size = listQuery.SafePageSize;
        var items = await store.ListAsync(query.Skip((page - 1) * size).Take(size), cancellationToken);
        return new PagedResult<TResult>(items.Select(map).ToArray(), total, page, size);
    }
}
