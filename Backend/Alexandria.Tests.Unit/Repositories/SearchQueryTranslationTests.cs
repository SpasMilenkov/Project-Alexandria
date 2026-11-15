using Alexandria.Data.Context;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Repositories;
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
    public void WhereNameMatches_translates_to_trigram_or_prefix_text_search()
    {
        using var context = TranslationContext();
        var terms = SearchTerms.Parse("summer vaca");

        var sql = context.Files.WhereNameMatches(terms).ToQueryString();

        sql.Should().Contain("similarity(f.\"NormalizedName\"");
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
    public void StartOfUtcDay_returns_midnight_in_utc()
    {
        var start = SearchDateFilters.StartOfUtcDay(new DateOnly(2026, 11, 14));

        start.Should().Be(new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc));
        start.Kind.Should().Be(DateTimeKind.Utc);
    }
}
