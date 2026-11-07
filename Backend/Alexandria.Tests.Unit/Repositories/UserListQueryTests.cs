using Alexandria.Data.Context;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Users;
using Alexandria.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Tests.Unit.Repositories;

public class UserListQueryTests
{
    private static AlexandriaDbContext TranslationContext()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=postgres;Password=postgres")
            .Options;
        return new AlexandriaDbContext(options);
    }

    private static string Sql(UserQueryDto query)
    {
        using var context = TranslationContext();

        return UserRepository.BuildUserListQuery(context, query).ToQueryString();
    }

    [Fact]
    public void Username_search_ignores_case_and_escapes_wildcards()
    {
        var sql = Sql(new UserQueryDto { UserName = "spas" });

        sql.Should().Contain("\"UserName\" ILIKE");
        sql.Should().Contain("ESCAPE '\\'");
    }

    [Fact]
    public void Email_search_ignores_case_and_escapes_wildcards()
    {
        var sql = Sql(new UserQueryDto { UserEmail = "spas@test.com" });

        sql.Should().Contain("\"Email\" ILIKE");
        sql.Should().Contain("ESCAPE '\\'");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_search_adds_no_text_filter(string? term)
    {
        var sql = Sql(new UserQueryDto { UserEmail = term, UserName = term });

        sql.Should().NotContain("ILIKE");
    }

    [Theory]
    [InlineData("spas", "%spas%")]
    [InlineData("  spas ", "%spas%")]
    [InlineData("a_b", "%a\\_b%")]
    [InlineData("100%", "%100\\%%")]
    [InlineData("a\\b", "%a\\\\b%")]
    public void ContainsPattern_treats_the_term_as_literal_text(string term, string expected)
    {
        UserRepository.ContainsPattern(term).Should().Be(expected);
    }

    [Theory]
    [InlineData(SortDirection.Asc, false)]
    [InlineData(SortDirection.Desc, true)]
    public void File_count_sort_counts_each_users_files(SortDirection direction, bool descending)
    {
        var sql = Sql(new UserQueryDto { SortBy = SortBy.FileCount, SortDirection = direction });

        var orderBy = sql[sql.IndexOf("ORDER BY", StringComparison.Ordinal)..];

        orderBy.Should().Contain("\"Files\"");
        orderBy.Should().Contain("\"UserName\"");
        orderBy.Contains("DESC").Should().Be(descending);
    }

    [Theory]
    [InlineData(SortDirection.Asc, false)]
    [InlineData(SortDirection.Desc, true)]
    public void Total_size_sort_sums_versions_of_files_that_are_not_deleted(
        SortDirection direction,
        bool descending)
    {
        var sql = Sql(new UserQueryDto { SortBy = SortBy.TotalSize, SortDirection = direction });

        var orderBy = sql[sql.IndexOf("ORDER BY", StringComparison.Ordinal)..];

        orderBy.Should().Contain("\"FileVersions\"");
        orderBy.Should().Contain("\"DeletedAt\" IS NULL");
        orderBy.Should().Contain("COALESCE");
        orderBy.Contains("DESC").Should().Be(descending);
    }

    // The frontend sends these as numbers from src/enums/SortBy.ts.
    [Theory]
    [InlineData(SortBy.Name, 0)]
    [InlineData(SortBy.CreatedAt, 1)]
    [InlineData(SortBy.UpdatedAt, 2)]
    [InlineData(SortBy.FileCount, 3)]
    [InlineData(SortBy.TotalSize, 4)]
    public void Sort_values_match_the_frontend_enum(SortBy sortBy, int expected)
    {
        ((int)sortBy).Should().Be(expected);
    }
}
