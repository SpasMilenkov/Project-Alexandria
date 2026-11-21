using Alexandria.Data.Context;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Repositories.Search;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Tests.Unit.Repositories;

public class SearchQueryTranslationTests
{
    private static AlexandriaDbContext TranslationContext()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=postgres;Password=postgres")
            .Options;
        return new AlexandriaDbContext(options);
    }

    [Fact]
    public void WhereNameMatches_translates_to_word_similarity_or_prefix_text_search()
    {
        using var context = TranslationContext();
        var terms = SearchTerms.Parse("summer vaca");

        var sql = context.Files.WhereNameMatches(terms).ToQueryString();

        // The query comes first: it is matched against the best stretch of the name, not the reverse.
        sql.Should().Contain("word_similarity(@trigramText, f.\"NormalizedName\") > 0.5");
        sql.Should().Contain("@@ to_tsquery('simple'");
    }

    [Fact]
    public void OrderByNameRelevance_orders_the_outer_query_after_paging_filters()
    {
        using var context = TranslationContext();
        var terms = SearchTerms.Parse("report");

        var sql = context.Files
            .Where(f => f.DeletedAt == null)
            .WhereNameMatches(terms)
            .OrderByNameRelevance(terms)
            .Skip(20)
            .Take(10)
            .ToQueryString();

        sql.Should().NotContain("FROM (");
        sql.Should().Contain("ts_rank(");

        var orderBy = sql[sql.LastIndexOf("ORDER BY", StringComparison.Ordinal)..];
        orderBy.Should().Contain("similarity(");
        orderBy.Should().Contain("f.\"Id\"");
        orderBy.Should().Contain("LIMIT");
    }

    [Fact]
    public void WhereNameOrContentMatches_checks_the_matched_ids_as_one_array_parameter()
    {
        using var context = TranslationContext();
        var terms = SearchTerms.Parse("paris invoice");
        Guid[] matching = [Guid.NewGuid(), Guid.NewGuid()];

        var sql = context.Files
            .WhereNameOrContentMatches(terms, matching)
            .ToQueryString();

        // One hashed array check per file; a join or a correlated subquery would cost per file.
        sql.Should().NotContain("JOIN");
        sql.Should().NotContain("EXISTS");

        var where = sql[sql.IndexOf("WHERE", StringComparison.Ordinal)..];
        where.Should().Contain("word_similarity(@trigramText, f.\"NormalizedName\")");
        where.Should().Contain("f.\"Id\" = ANY (");
    }

    [Fact]
    public void OrderByNameAndContentRelevance_adds_fixed_boosts_for_name_words_and_text_matches()
    {
        using var context = TranslationContext();
        var terms = SearchTerms.Parse("paris invoice");
        Guid[] matching = [Guid.NewGuid()];

        var sql = context.Files
            .Where(f => f.OwnerId == Guid.Empty)
            .WhereNameOrContentMatches(terms, matching)
            .OrderByNameAndContentRelevance(terms, matching)
            .Skip(0)
            .Take(20)
            .ToQueryString();

        sql.Should().NotContain("JOIN");
        sql.Should().NotContain("EXISTS");

        var orderBy = sql[sql.LastIndexOf("ORDER BY", StringComparison.Ordinal)..];
        orderBy.Should().Contain("similarity(");
        orderBy.Should().Contain("CASE");
        orderBy.Should().Contain("THEN 0.5");
        orderBy.Should().Contain("THEN 1.0");
        orderBy.Should().Contain("f.\"Id\" = ANY (");
        orderBy.Should().Contain("LIMIT");
    }

    [Fact]
    public void OrderByNameRelevance_boosts_names_holding_every_word()
    {
        using var context = TranslationContext();
        var terms = SearchTerms.Parse("paris");

        var sql = context.Files
            .WhereNameMatches(terms)
            .OrderByNameRelevance(terms)
            .ToQueryString();

        var orderBy = sql[sql.LastIndexOf("ORDER BY", StringComparison.Ordinal)..];
        orderBy.Should().Contain("CASE");
        orderBy.Should().Contain("@@ to_tsquery('simple'");
        orderBy.Should().Contain("THEN 1.0");
    }

    [Theory]
    [InlineData("paris invoice", "paris:* & invoice:*", true)]
    [InlineData("an invoice of 42", "invoice:*", true)]
    [InlineData("in", "", false)]
    [InlineData("q3 ok", "", false)]
    [InlineData("Café-Menü", "café:* & menü:*", true)]
    public void ContentTsQuery_prefixesWordsOfThreeOrMoreCharacters(string query, string expected, bool searches)
    {
        var terms = SearchTerms.Parse(query);

        terms.ContentTsQuery.Should().Be(expected);
        terms.SearchesContent.Should().Be(searches);
    }

    [Theory]
    [InlineData("paris invoice", @"\m(paris|invoice)")]
    [InlineData("ML-KEM", @"\m(kem)")]
    [InlineData("file.name*(draft)|v2", @"\m(file|name|draft)")]
    [InlineData("Изкуствен", @"\m(изкуствен)")]
    public void ContentWordStartPattern_alternatesTheContentWords_withoutRegexSyntaxFromTheQuery(
        string query, string expected)
    {
        SearchTerms.Parse(query).ContentWordStartPattern.Should().Be(expected);
    }

    [Fact]
    public void WhereDeletedWithin_filters_on_the_deletion_timestamp()
    {
        using var context = TranslationContext();

        var sql = context.Directories
            .WhereDeletedWithin(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 14))
            .ToQueryString();

        var where = sql[sql.IndexOf("WHERE", StringComparison.Ordinal)..];
        where.Should().Contain("d.\"DeletedAt\" >=");
        where.Should().Contain("d.\"DeletedAt\" <");
        where.Should().NotContain("\"CreatedAt\"");
    }

    [Fact]
    public void WhereCreatedWithin_last_representable_day_skips_the_upper_bound()
    {
        using var context = TranslationContext();

        var sql = context.Files
            .WhereCreatedWithin(null, DateOnly.MaxValue)
            .ToQueryString();

        sql.Should().NotContain("WHERE");
    }

    [Fact]
    public void OrderByRequested_breaks_ties_on_id()
    {
        using var context = TranslationContext();

        var sql = context.Directories
            .OrderByRequested(SortBy.UpdatedAt, SortDirection.Desc)
            .ToQueryString();

        sql.Should().Contain("ORDER BY d.\"UpdatedAt\" DESC, d.\"Id\"");
    }

    [Fact]
    public void WhereHasAllTags_adds_one_live_tag_check_per_tag()
    {
        using var context = TranslationContext();

        var sql = context.Files
            .WhereHasAllTags([Guid.NewGuid(), Guid.NewGuid()])
            .ToQueryString();

        var where = sql[sql.IndexOf("WHERE", StringComparison.Ordinal)..];
        where.Split("EXISTS").Should().HaveCount(3);
        where.Should().Contain("\"Source\" <>");
        where.Should().Contain("\"DeletedAt\" IS NULL");
    }

    [Fact]
    public void StartOfUtcDay_returns_midnight_in_utc()
    {
        var start = SearchDateFilters.StartOfUtcDay(new DateOnly(2026, 11, 14));

        start.Should().Be(new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc));
        start.Kind.Should().Be(DateTimeKind.Utc);
    }
}