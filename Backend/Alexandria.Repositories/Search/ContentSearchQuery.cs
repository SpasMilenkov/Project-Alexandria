using Alexandria.Data.Context;
using Microsoft.EntityFrameworkCore;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Repositories.Search;

/// <summary>
/// File search over names and document text together. The text side runs first, on its own,
/// through the <c>matching_document_files</c> SQL function (Sql/DocumentText), and its file ids go
/// into the page and count queries as one array parameter, which Postgres hashes once. Measured on
/// 20,000 files and 3,000 documents that all matched: about 120 ms in total, against 2.5 s for a
/// join with a per-file text rank.
/// </summary>
internal static class ContentSearchQuery
{
    /// <summary>
    /// What a text match adds to a file's name relevance. It beats a fuzzy name match up to
    /// similarity 0.25 (scoring 0.5), which covers the noise band, while a name holding every
    /// word gets <see cref="NameSearchQuery.NameWordMatchBoost"/> and stays on top. At 0.25 the
    /// text match lost to names at similarity 0.14 ("ML-KEM" ranked "Kecon-Gi.mp3" above the
    /// slides that define it). A fixed amount, because ranking each matching document by its
    /// text cost 0.3 ms apiece and grew with the library.
    /// </summary>
    internal const double ContentMatchBoost = 0.5;

    /// <summary>
    /// The owner's files, trashed ones included, whose current version's text contains every
    /// content word of the query.
    /// </summary>
    internal static async Task<Guid[]> MatchingFileIdsAsync(
        this AlexandriaDbContext context,
        Guid ownerId,
        SearchTerms terms,
        CancellationToken ct)
    {
        var tsQuery = terms.ContentTsQuery;

        var ids = await context.Database
            .SqlQuery<Guid>($"""SELECT m."FileId" AS "Value" FROM matching_document_files({ownerId}, {tsQuery}) AS m""")
            .ToListAsync(ct);

        return [.. ids];
    }

    extension(IQueryable<File> query)
    {
        internal IQueryable<File> WhereNameOrContentMatches(SearchTerms terms,
            Guid[] matchingFileIds)
        {
            var trigramText = terms.TrigramText;
            var tsQuery = terms.PrefixTsQuery;

            return query.Where(f =>
                EF.Functions.TrigramsWordSimilarity(trigramText, f.NormalizedName) > NameSearchQuery.MinWordSimilarity
                || f.SearchVector.Matches(EF.Functions.ToTsQuery("simple", tsQuery))
                || ((IEnumerable<Guid>)matchingFileIds).Contains(f.Id));
        }

        /// <summary>
        /// Name relevance as in <see cref="NameSearchQuery.OrderByNameRelevance{T}"/>, plus
        /// <see cref="ContentMatchBoost"/> for files whose text matches. Tiers: names holding
        /// every word, then text matches, then names that are only close.
        /// </summary>
        internal IOrderedQueryable<File> OrderByNameAndContentRelevance(SearchTerms terms,
            Guid[] matchingFileIds)
        {
            var trigramText = terms.TrigramText;
            var tsQuery = terms.PrefixTsQuery;

            return query
                .OrderByDescending(f =>
                    EF.Functions.TrigramsSimilarity(f.NormalizedName, trigramText) * NameSearchQuery.SimilarityWeight
                    + f.SearchVector.Rank(EF.Functions.ToTsQuery("simple", tsQuery))
                    + (f.SearchVector.Matches(EF.Functions.ToTsQuery("simple", tsQuery))
                        ? NameSearchQuery.NameWordMatchBoost
                        : 0.0)
                    + (((IEnumerable<Guid>)matchingFileIds).Contains(f.Id) ? ContentMatchBoost : 0.0))
                .ThenBy(f => f.Name)
                .ThenBy(f => f.Id);
        }
    }
}