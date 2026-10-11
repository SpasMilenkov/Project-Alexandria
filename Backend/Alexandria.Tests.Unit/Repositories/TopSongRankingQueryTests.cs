using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.Repositories;

public class TopSongRankingQueryTests
{
    private static readonly Guid Owner = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid FileA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FileB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FileC = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime From = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = From.AddDays(7);

    private static StreamSession Session(Guid fileId, long seconds, bool qualified) => new()
    {
        Id = Guid.NewGuid(),
        StartedAt = From,
        EndedAt = From.AddMinutes(1),
        ListenedSeconds = seconds,
        IsQualifiedPlay = qualified,
        StreamHistory = new StreamHistory
        {
            UserId = Owner,
            FileId = fileId,
            File = new File { Id = fileId, Name = "song.mp3", MimeType = "audio/mpeg", OwnerId = Owner }
        }
    };

    private static TranspilationJob PlayableJob(Guid fileId) => new()
    {
        FileVersion = new FileVersion
        {
            Id = Guid.NewGuid(), FileId = fileId, Size = 100, VersionNumber = 1, MimeType = "audio/mpeg",
            File = new File
            {
                Id = fileId, Name = "song.mp3", MimeType = "audio/mpeg", OwnerId = Owner,
                CurrentVersionId = Guid.NewGuid()
            }
        }
    };

    [Fact]
    public void TopSongRankingsQuery_counts_stored_qualification_sums_all_seconds_and_keeps_stable_ties()
    {
        var sessions = new[]
        {
            Session(FileC, 1000, true), Session(FileB, 10, true), Session(FileB, 10, true),
            Session(FileA, 10, true), Session(FileA, 10, true), Session(FileA, 100, false)
        }.AsQueryable();
        var ids = new[] { FileA, FileB, FileC }.AsQueryable();

        var ranking = StreamHistoryRepository.TopSongRankingsQuery(sessions, ids, Owner, From, To, 3).ToList();

        ranking.Select(r => r.FileId).Should().Equal(FileA, FileB, FileC);
        ranking.Select(r => r.QualifiedPlayCount).Should().Equal(2, 2, 1);
        ranking.Select(r => r.TotalListenedSeconds).Should().Equal(120, 20, 1000);

        sessions = new[] { Session(FileB, 20, true), Session(FileA, 20, true) }.AsQueryable();
        ranking = StreamHistoryRepository.TopSongRankingsQuery(sessions, ids, Owner, From, To, 3).ToList();

        ranking.Select(r => r.FileId).Should().Equal(FileA, FileB);
    }

    [Fact]
    public void TopSongRankingsQuery_excludes_unplayable_and_unqualified_files_before_limit_without_duplicate_plays()
    {
        var sessions = new[]
        {
            Session(FileA, 50, true), Session(FileA, 50, true), Session(FileB, 10, true),
            Session(FileC, 1000, false)
        }.AsQueryable();
        var ids = new[] { FileB, FileB, FileC }.AsQueryable();

        var ranking = StreamHistoryRepository.TopSongRankingsQuery(sessions, ids, Owner, From, To, 1).ToList();

        ranking.Should().ContainSingle();
        ranking[0].FileId.Should().Be(FileB);
        ranking[0].QualifiedPlayCount.Should().Be(1);
    }

    [Fact]
    public void PlayableAudioFileIdsQuery_allows_fallback_versions_and_excludes_deleted_foreign_or_versionless_files()
    {
        var fallback = PlayableJob(FileA);
        var deletedVersion = PlayableJob(FileB);
        deletedVersion.FileVersion.DeletedAt = From;
        var deletedFile = PlayableJob(FileB);
        deletedFile.FileVersion.File.DeletedAt = From;
        var foreign = PlayableJob(FileB);
        foreign.FileVersion.File.OwnerId = Guid.NewGuid();
        var versionless = PlayableJob(FileC);
        versionless.FileVersion.File.CurrentVersionId = null;

        var ids = StreamHistoryRepository.PlayableAudioFileIdsQuery(
            new[] { fallback, deletedVersion, deletedFile, foreign, versionless }.AsQueryable(), Owner).ToList();

        fallback.FileVersion.Id.Should().NotBe(fallback.FileVersion.File.CurrentVersionId!.Value);
        ids.Should().Equal(FileA);
    }

    [Fact]
    public void TopSongsQuery_translates_aggregates_playback_guards_ordering_and_limit_with_npgsql()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=postgres;Password=postgres")
            .Options;
        using var context = new AlexandriaDbContext(options);
        var repository = new StreamHistoryRepository(context);

        var sql = repository.TopSongsQuery(Owner, From, To, 5).ToQueryString();

        sql.Should().Contain("GROUP BY").And.Contain("HAVING").And.Contain("count(")
            .And.Contain("sum(").And.Contain("ORDER BY").And.Contain("LIMIT")
            .And.Contain("IsQualifiedPlay").And.Contain("ListenedSeconds")
            .And.Contain("EndedAt\" IS NOT NULL").And.Contain("DeletedAt\" IS NULL")
            .And.Contain("CurrentVersionId\" IS NOT NULL").And.Contain("IsVideo")
            .And.Contain("Ready").And.Contain("UserId").And.Contain("OwnerId");
        sql.Should().NotContain("MediaMetadata");
    }
}
