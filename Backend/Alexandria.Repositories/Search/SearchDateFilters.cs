using Alexandria.Data.Models;

namespace Alexandria.Repositories.Search;

/// <summary>
/// Calendar-day filters on the audit timestamps. Days are UTC days and both ends are
/// inclusive, so "before the 14th" still returns everything that happened on the 14th.
/// </summary>
internal static class SearchDateFilters
{
    internal static IQueryable<T> WhereCreatedWithin<T>(this IQueryable<T> query, DateOnly? after,
        DateOnly? before)
        where T : class, IBase
    {
        if (after.HasValue)
        {
            var start = StartOfUtcDay(after.Value);
            query = query.Where(e => e.CreatedAt >= start);
        }

        if (HasEnd(before))
        {
            var end = StartOfUtcDay(before!.Value.AddDays(1));
            query = query.Where(e => e.CreatedAt < end);
        }

        return query;
    }

    internal static IQueryable<T> WhereUpdatedWithin<T>(this IQueryable<T> query, DateOnly? after,
        DateOnly? before)
        where T : class, IBase
    {
        if (after.HasValue)
        {
            var start = StartOfUtcDay(after.Value);
            query = query.Where(e => e.UpdatedAt >= start);
        }

        if (HasEnd(before))
        {
            var end = StartOfUtcDay(before!.Value.AddDays(1));
            query = query.Where(e => e.UpdatedAt < end);
        }

        return query;
    }

    internal static IQueryable<T> WhereDeletedWithin<T>(this IQueryable<T> query, DateOnly? after,
        DateOnly? before)
        where T : class, IBase
    {
        if (after.HasValue)
        {
            var start = StartOfUtcDay(after.Value);
            query = query.Where(e => e.DeletedAt >= start);
        }

        if (HasEnd(before))
        {
            var end = StartOfUtcDay(before!.Value.AddDays(1));
            query = query.Where(e => e.DeletedAt < end);
        }

        return query;
    }

    internal static DateTime StartOfUtcDay(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    // The last representable day has no next day to bound against, and every row is
    // before it anyway.
    private static bool HasEnd(DateOnly? before) => before.HasValue && before.Value < DateOnly.MaxValue;
}