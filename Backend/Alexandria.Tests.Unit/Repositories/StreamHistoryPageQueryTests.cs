using System.Text.RegularExpressions;
using Alexandria.Data.Context;
using Alexandria.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Tests.Unit.Repositories;

public class StreamHistoryPageQueryTests
{
    [Fact]
    public void HistoryPageQuery_with_media_metadata_reuses_one_join_per_table()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=postgres;Password=postgres")
            .Options;
        using var context = new AlexandriaDbContext(options);

        var sql = StreamHistoryRepository.HistoryPageQuery(context.StreamHistories, 2, 6).ToQueryString();

        sql.Should().Contain("\"Artist\"").And.Contain("\"Album\"").And.Contain("\"Duration\"");
        sql.Should().Contain("video/");
        Regex.Matches(sql, "JOIN \"Files\"").Should().HaveCount(1);
        Regex.Matches(sql, "JOIN \"MediaMetadata\"").Should().HaveCount(1);
    }
}
