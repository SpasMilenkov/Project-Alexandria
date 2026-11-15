using System.Linq.Expressions;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Repositories;

/// <summary>
/// The user-chosen ordering for file and directory listings when no search text is given.
/// The id tiebreak keeps pages stable when many rows share a name or timestamp.
/// </summary>
internal static class SearchSortQuery
{
    internal static IOrderedQueryable<T> OrderByRequested<T>(this IQueryable<T> query, SortBy sortBy,
        SortDirection direction)
        where T : class, ISearchableByName, IBase
    {
        var ascending = direction == SortDirection.Asc;

        var ordered = sortBy switch
        {
            SortBy.CreatedAt => OrderBy(query, e => e.CreatedAt, ascending),
            SortBy.UpdatedAt => OrderBy(query, e => e.UpdatedAt, ascending),
            _ => OrderBy(query, e => e.Name, ascending)
        };

        return ordered.ThenBy(e => e.Id);
    }

    private static IOrderedQueryable<T> OrderBy<T, TKey>(IQueryable<T> query, Expression<Func<T, TKey>> key,
        bool ascending)
    {
        if (ascending)
            return query.OrderBy(key);

        return query.OrderByDescending(key);
    }
}
