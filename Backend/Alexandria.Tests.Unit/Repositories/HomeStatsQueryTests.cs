using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.Repositories;

public class HomeStatsQueryTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateTime From = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = From.AddDays(7);

    private static AlexandriaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=postgres;Password=postgres")
            .Options;

        return new AlexandriaDbContext(options);
    }

    private static StreamSession Session()
    {
        return new StreamSession
        {
            Id = Guid.NewGuid(), StartedAt = From, EndedAt = From.AddMinutes(1), ListenedSeconds = 60,
            IsQualifiedPlay = true,
            StreamHistory = new StreamHistory
            {
                UserId = Owner, FileId = Guid.NewGuid(),
                File = new File { Name = "song.mp3", MimeType = "audio/mpeg", OwnerId = Owner }
            }
        };
    }

    [Fact]
    public void ListeningSessionRowsQuery_excludes_foreign_deleted_live_video_and_out_of_range_sessions()
    {
        var included = Session();
        var atEnd = Session();
        atEnd.StartedAt = To;
        var beforeStart = Session();
        beforeStart.StartedAt = From.AddTicks(-1);
        var foreign = Session();
        foreign.StreamHistory.UserId = Guid.NewGuid();
        var live = Session();
        live.EndedAt = null;
        var deletedSession = Session();
        deletedSession.DeletedAt = From;
        var deletedHistory = Session();
        deletedHistory.StreamHistory.DeletedAt = From;
        var deletedFile = Session();
        deletedFile.StreamHistory.File.DeletedAt = From;
        var video = Session();
        video.StreamHistory.File = new File { Name = "video.mp4", MimeType = "video/mp4", OwnerId = Owner };
        var zero = Session();
        zero.ListenedSeconds = 0;

        var sessions = new[] { included, atEnd, beforeStart, foreign, live, deletedSession, deletedHistory,
            deletedFile, video, zero }.AsQueryable();
        var result = StreamHistoryRepository.ListeningSessionRowsQuery(sessions, Owner, From, To).ToList();

        result.Should().ContainSingle().Which.SessionId.Should().Be(included.Id);
        result[0].QualifiedPlay.Should().BeTrue();
    }

    [Fact]
    public void ListeningSessionRowsQuery_translates_owner_range_closed_audio_and_deletion_filters_with_npgsql()
    {
        using var context = CreateContext();
        var sql = StreamHistoryRepository.ListeningSessionRowsQuery(
            context.StreamSessions.AsNoTracking(), Owner, From, To).ToQueryString();

        sql.Should().Contain("UserId").And.Contain("audio/").And.Contain("StartedAt")
            .And.Contain("EndedAt\" IS NOT NULL").And.Contain("IsQualifiedPlay")
            .And.Contain("DeletedAt\" IS NULL");
    }
}
