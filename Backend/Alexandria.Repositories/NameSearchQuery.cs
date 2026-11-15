using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

/// <summary>
/// Name matching shared by file and directory search. Trigram similarity tolerates typos
/// and partial words, and the prefix text-search query catches a word the user is still
/// typing. Ordering is applied to the outer query, so filters and paging never lose it.
/// </summary>
internal static class NameSearchQuery
{
    internal const double MinSimilarity = 0.1;

    // Closeness of the whole name counts double, so a near-exact name outranks a long
    // name that merely contains every word.
    internal const double SimilarityWeight = 2.0;

    internal static IQueryable<T> WhereNameMatches<T>(this IQueryable<T> query, SearchTerms terms)
        where T : class, ISearchableByName
    {
        var trigramText = terms.TrigramText;
        var tsQuery = terms.PrefixTsQuery;

        return query.Where(e =>
            EF.Functions.TrigramsSimilarity(e.NormalizedName, trigramText) > MinSimilarity
            || e.SearchVector.Matches(EF.Functions.ToTsQuery("simple", tsQuery)));
    }

    internal static IOrderedQueryable<T> OrderByNameRelevance<T>(this IQueryable<T> query, SearchTerms terms)
        where T : class, ISearchableByName
    {
        var trigramText = terms.TrigramText;
        var tsQuery = terms.PrefixTsQuery;

        return query
            .OrderByDescending(e =>
                EF.Functions.TrigramsSimilarity(e.NormalizedName, trigramText) * SimilarityWeight
                + e.SearchVector.Rank(EF.Functions.ToTsQuery("simple", tsQuery)))
            .ThenBy(e => e.Name)
            .ThenBy(e => e.Id);
    }
}
