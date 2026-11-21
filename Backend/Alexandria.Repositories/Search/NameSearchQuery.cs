using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories.Search;

/// <summary>
/// Name matching shared by file and directory search. Trigram word similarity tolerates typos
/// and partial words, and the prefix text-search query catches a word the user is still
/// typing. Ordering is applied to the outer query, so filters and paging never lose it.
/// </summary>
internal static class NameSearchQuery
{
    /// <summary>
    /// How close the query must be to some stretch of the name. Whole-name similarity was the
    /// filter before, at 0.1, and let through mostly noise ("invoice" matched 104 unrelated
    /// names), while raising it to 0.2 dropped typos in long names ("robinsn" against a
    /// Robinson Crusoe epub scores 0.10). Word similarity scores those typos 0.58 to 0.75 and
    /// lets through about as much noise as 0.2 did, for about 3 ms more on 20,000 files.
    /// </summary>
    internal const double MinWordSimilarity = 0.5;

    // Closeness of the whole name counts double, so a near-exact name outranks a long
    // name that merely contains every word.
    internal const double SimilarityWeight = 2.0;

    /// <summary>
    /// What a name holding every typed word (as a prefix) adds over closeness alone. A long name
    /// with the word in it is barely similar as a whole ("…trip to paris.jpg" scores 0.11 for
    /// "paris"), so containing the words is what puts a name on top.
    /// </summary>
    internal const double NameWordMatchBoost = 1.0;

    internal static IQueryable<T> WhereNameMatches<T>(this IQueryable<T> query, SearchTerms terms)
        where T : class, ISearchableByName
    {
        var trigramText = terms.TrigramText;
        var tsQuery = terms.PrefixTsQuery;

        return query.Where(e =>
            EF.Functions.TrigramsWordSimilarity(trigramText, e.NormalizedName) > MinWordSimilarity
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
                + e.SearchVector.Rank(EF.Functions.ToTsQuery("simple", tsQuery))
                + (e.SearchVector.Matches(EF.Functions.ToTsQuery("simple", tsQuery)) ? NameWordMatchBoost : 0.0))
            .ThenBy(e => e.Name)
            .ThenBy(e => e.Id);
    }
}