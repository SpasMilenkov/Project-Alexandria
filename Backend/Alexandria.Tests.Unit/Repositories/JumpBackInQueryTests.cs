using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.Repositories;

public class JumpBackInQueryTests
{
    private static readonly Guid Owner = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid FileA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FileB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FileC = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime Cutoff = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    private static StreamHistory History(Guid fileId, int plays = 2, long seconds = 120) => new()
    {
        UserId = Owner,
        FileId = fileId,
        File = new File { Id = fileId, OwnerId = Owner, Name = "song.mp3", MimeType = "audio/mpeg" },
        QualifiedPlayCount = plays,
        TotalListenedSeconds = seconds,
        LastPlayedAt = Cutoff.AddDays(-1),
        LastAccessedAt = Cutoff.AddDays(-1)
    };

    [Fact]
    public void JumpBackInRankingsQuery_ranks_persisted_plays_then_seconds_and_limits_without_duplicates()
    {
        var histories = new[] { History(FileA, 3, 500), History(FileB, 5, 100), History(FileC, 3, 600) };
        var playable = new[] { FileA, FileB, FileB, FileC }.AsQueryable();

        var result = StreamHistoryRepository.JumpBackInRankingsQuery(
            histories.AsQueryable(), playable, Owner, Cutoff, 2).ToList();

        result.Select(r => r.FileId).Should().Equal(FileB, FileC);
        result.Select(r => r.QualifiedPlayCount).Should().Equal(5, 3);
        result.Select(r => r.TotalListenedSeconds).Should().Equal(100, 600);
        result.Should().OnlyContain(r => r.LastPlayedAt == Cutoff.AddDays(-1));
    }

    [Fact]
    public void JumpBackInRankingsQuery_breaks_equal_rankings_by_oldest_access_then_file_id()
    {
        var oldest = History(FileC);
        oldest.LastAccessedAt = Cutoff.AddDays(-2);
        var histories = new[] { History(FileB), oldest, History(FileA) }.AsQueryable();

        var result = StreamHistoryRepository.JumpBackInRankingsQuery(
            histories, new[] { FileA, FileB, FileC }.AsQueryable(), Owner, Cutoff, 3).ToList();

        result.Select(r => r.FileId).Should().Equal(FileC, FileA, FileB);
    }

    [Theory]
    [InlineData("foreign-history")]
    [InlineData("deleted-history")]
    [InlineData("deleted-file")]
    [InlineData("video")]
    [InlineData("one-play")]
    [InlineData("no-listening")]
    [InlineData("missing-play-date")]
    [InlineData("play-at-cutoff")]
    [InlineData("access-at-cutoff")]
    [InlineData("recent-brief-access")]
    [InlineData("unplayable")]
    public void JumpBackInRankingsQuery_excludes_ineligible_history_before_applying_limit(string condition)
    {
        var excluded = History(FileA, 100, 10_000);
        var playable = new[] { FileA, FileB };

        switch (condition)
        {
            case "foreign-history": excluded.UserId = Guid.NewGuid(); break;
            case "deleted-history": excluded.DeletedAt = Cutoff; break;
            case "deleted-file": excluded.File.DeletedAt = Cutoff; break;
            case "video":
                excluded.File = new File { Id = FileA, OwnerId = Owner, Name = "film.mp4", MimeType = "video/mp4" };
                break;
            case "one-play": excluded.QualifiedPlayCount = 1; break;
            case "no-listening": excluded.TotalListenedSeconds = 0; break;
            case "missing-play-date": excluded.LastPlayedAt = null; break;
            case "play-at-cutoff": excluded.LastPlayedAt = Cutoff; break;
            case "access-at-cutoff": excluded.LastAccessedAt = Cutoff; break;
            case "recent-brief-access": excluded.LastAccessedAt = Cutoff.AddDays(1); break;
            case "unplayable": playable = [FileB]; break;
            default: throw new ArgumentOutOfRangeException(nameof(condition));
        }

        var result = StreamHistoryRepository.JumpBackInRankingsQuery(
            new[] { excluded, History(FileB) }.AsQueryable(), playable.AsQueryable(), Owner, Cutoff, 1).ToList();

        result.Should().ContainSingle().Which.FileId.Should().Be(FileB);
    }

    [Fact]
    public void JumpBackInQuery_translates_guards_ordering_and_limit_without_fetching_sessions()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=postgres;Password=postgres")
            .Options;
        using var context = new AlexandriaDbContext(options);
        var repository = new StreamHistoryRepository(context);

        var sql = repository.JumpBackInQuery(Owner, Cutoff, 6).ToQueryString();

        sql.Should().Contain("QualifiedPlayCount").And.Contain("TotalListenedSeconds")
            .And.Contain("LastPlayedAt").And.Contain("LastAccessedAt")
            .And.Contain("ORDER BY").And.Contain("LIMIT")
            .And.Contain("UserId").And.Contain("OwnerId").And.Contain("audio/")
            .And.Contain("DeletedAt\" IS NULL").And.Contain("CurrentVersionId\" IS NOT NULL")
            .And.Contain("Ready").And.Contain("IsVideo");
        sql.Should().NotContain("StreamSession").And.NotContain("MediaMetadata");
    }
}
