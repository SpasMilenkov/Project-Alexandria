using Alexandria.Data.Context;
using Alexandria.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Tests.Unit.Repositories;

public class ListeningHistoryQueryTests
{
    [Fact]
    public void ListeningHistoryRowsQuery_with_npgsql_translates_without_connecting()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=postgres;Password=postgres")
            .Options;
        using var context = new AlexandriaDbContext(options);
        var repository = new StreamHistoryRepository(context);

        // Exercises the real provider and repository expression, not a LINQ-to-Objects substitute.
        var sql = repository.ListeningHistoryRowsQuery(Guid.Parse("11111111-1111-1111-1111-111111111111"))
            .ToQueryString();

        sql.Should().Contain("SELECT").And.Contain("audio/").And.Contain("UserId");
        sql.Should().NotContain("GROUP BY");
    }

    [Fact]
    public void BuildListeningHistoryRefs_with_unsorted_rows_keeps_first_and_latest_strictly_before_cutoff()
    {
        var first = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var second = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var cutoff = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        StreamHistoryRepository.ListeningHistorySessionRow[] rows =
        [
            new(second, null, cutoff),
            new(first, "Artist", cutoff.AddDays(1)),
            new(first, "Artist", cutoff.AddDays(-1)),
            new(first, "Artist", cutoff.AddDays(-20)),
            new(second, null, cutoff.AddDays(2))
        ];

        var result = StreamHistoryRepository.BuildListeningHistoryRefs(rows, cutoff);

        result.Should().HaveCount(2);
        result[0].FileId.Should().Be(first);
        result[0].HistoryCreatedAt.Should().Be(cutoff.AddDays(-20));
        result[0].LastBeforePeriod.Should().Be(cutoff.AddDays(-1));
        result[1].LastBeforePeriod.Should().BeNull();
        StreamHistoryRepository.BuildListeningHistoryRefs([], cutoff).Should().BeEmpty();
    }
}