using Alexandria.Data.Models.Enumerators;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Repositories;

/// <summary>
/// Tag filters shared by the general file search and the tag search. A tag counts only
/// while it is live and its assignment was not suppressed by the user.
/// </summary>
internal static class FileTagFilters
{
    internal static IQueryable<File> WhereHasAllTags(this IQueryable<File> query, ICollection<Guid> tagIds)
    {
        foreach (var tagId in tagIds)
        {
            var currentTagId = tagId;
            query = query.Where(f =>
                f.FileTags.Any(ft =>
                    ft.TagId == currentTagId &&
                    ft.Source != TagSource.Suppressed &&
                    ft.Tag.DeletedAt == null));
        }

        return query;
    }
}
