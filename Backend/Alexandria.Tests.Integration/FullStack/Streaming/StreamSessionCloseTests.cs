using System.Net;
using System.Net.Http.Json;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Repositories;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Streaming;

public class StreamSessionCloseTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static DateTime SeedBase => DateOnly.FromDateTime(DateTime.UtcNow)
        .ToDateTime(new TimeOnly(0, 0), DateTimeKind.Utc).AddHours(-1);

    private async Task<(Guid HistoryId, Guid[] SessionIds)> SeedHistoryAsync(int sessionCount = 2)
    {
        await using var db = Fixture.CreateDbContext();
        var ct = TestContext.Current.CancellationToken;
        var seed = await StreamingSeedHelper.SeedStreamableFileAsync(
            db, UserId, "close-test.mp3", SeedBase, 600, ct: ct);

        var history = new StreamHistory
        {
            Id = Guid.NewGuid(), UserId = UserId, FileId = seed.FileId,
            TotalListenedSeconds = 100, LastAccessedAt = SeedBase, CreatedAt = SeedBase,
        };

        db.StreamHistories.Add(history);

        var sessions = Enumerable.Range(0, sessionCount).Select(_ => new StreamSession
        {
            Id = Guid.NewGuid(), StreamHistoryId = history.Id,
            StartedAt = SeedBase, CreatedAt = SeedBase,
        }).ToArray();

        db.StreamSessions.AddRange(sessions);
        await db.SaveChangesAsync(ct);

        return (history.Id, sessions.Select(s => s.Id).ToArray());
    }

    private static async Task<StreamSession> PrepareCloseAsync(
        StreamHistoryRepository repository, Guid sessionId, long listenedSeconds,
        long endPositionSeconds, bool finished, DateTime closedAt)
    {
        var session = await repository.GetSessionByIdAsync(sessionId, TestContext.Current.CancellationToken);
        session.Should().NotBeNull();

        session.EndedAt = closedAt;
        session.EndPositionSeconds = endPositionSeconds;
        session.ListenedSeconds = listenedSeconds;
        session.IsQualifiedPlay = listenedSeconds >= 30;
        session.PlaybackFinished = finished;

        return session;
    }

    [Fact]
    public async Task CloseSessionAsync_concurrent_sessions_preserve_both_increments_and_latest_position()
    {
        var (historyId, sessionIds) = await SeedHistoryAsync();
        var ct = TestContext.Current.CancellationToken;
        await using var firstDb = Fixture.CreateDbContext();
        await using var secondDb = Fixture.CreateDbContext();
        var firstRepository = new StreamHistoryRepository(firstDb);
        var secondRepository = new StreamHistoryRepository(secondDb);

        var firstSnapshot = await firstDb.StreamHistories.SingleAsync(h => h.Id == historyId, ct);
        var secondSnapshot = await secondDb.StreamHistories.SingleAsync(h => h.Id == historyId, ct);
        firstSnapshot.TotalListenedSeconds.Should().Be(100);
        secondSnapshot.TotalListenedSeconds.Should().Be(100);

        var first = await PrepareCloseAsync(firstRepository, sessionIds[0], 30, 600, true,
            SeedBase.AddMinutes(2));
        var second = await PrepareCloseAsync(secondRepository, sessionIds[1], 40, 40, false,
            SeedBase.AddMinutes(1));

        await Task.WhenAll(
            firstRepository.CloseSessionAsync(first, UserId, ct),
            secondRepository.CloseSessionAsync(second, UserId, ct));

        await using var verificationDb = Fixture.CreateDbContext();
        var history = await verificationDb.StreamHistories.SingleAsync(h => h.Id == historyId, ct);
        var sessions = await verificationDb.StreamSessions.Where(s => s.StreamHistoryId == historyId).ToListAsync(ct);

        history.TotalListenedSeconds.Should().Be(170);
        history.QualifiedPlayCount.Should().Be(2);
        history.HasFinished.Should().BeTrue();
        history.MaxPositionReachedSeconds.Should().Be(600);
        history.PositionSeconds.Should().Be(600);
        history.LastAccessedAt.Should().Be(first.EndedAt!.Value);
        history.LastPlayedAt.Should().Be(first.EndedAt);
        sessions.Should().OnlyContain(s => s.EndedAt != null && s.IsQualifiedPlay);
        sessions.Sum(s => s.ListenedSeconds).Should().Be(70);
    }

    [Fact]
    public async Task CloseSessionAsync_concurrent_duplicates_and_changed_retries_count_once()
    {
        var (_, sessionIds) = await SeedHistoryAsync(1);
        var ct = TestContext.Current.CancellationToken;
        await using var firstDb = Fixture.CreateDbContext();
        await using var secondDb = Fixture.CreateDbContext();
        var firstRepository = new StreamHistoryRepository(firstDb);
        var secondRepository = new StreamHistoryRepository(secondDb);

        var first = await PrepareCloseAsync(firstRepository, sessionIds[0], 30, 30, false,
            SeedBase.AddMinutes(1));
        var second = await PrepareCloseAsync(secondRepository, sessionIds[0], 40, 600, true,
            SeedBase.AddMinutes(2));

        var results = await Task.WhenAll(
            firstRepository.CloseSessionAsync(first, UserId, ct),
            secondRepository.CloseSessionAsync(second, UserId, ct));

        await using var verificationDb = Fixture.CreateDbContext();
        var saved = await verificationDb.StreamSessions.AsNoTracking().SingleAsync(s => s.Id == sessionIds[0], ct);
        var expectedTotal = 100 + saved.ListenedSeconds;

        saved.ListenedSeconds.Should().BeOneOf(30, 40);
        results.Should().OnlyContain(h => h.TotalListenedSeconds == expectedTotal && h.QualifiedPlayCount == 1);

        var retryRepository = new StreamHistoryRepository(verificationDb);
        var retry = await PrepareCloseAsync(retryRepository, sessionIds[0], 600, 600, true,
            SeedBase.AddMinutes(3));
        var history = await retryRepository.CloseSessionAsync(retry, UserId, ct);
        var afterRetry = await verificationDb.StreamSessions.AsNoTracking().SingleAsync(s => s.Id == sessionIds[0], ct);

        history.TotalListenedSeconds.Should().Be(expectedTotal);
        history.QualifiedPlayCount.Should().Be(1);
        history.HasFinished.Should().Be(saved.PlaybackFinished);
        history.PositionSeconds.Should().Be(saved.EndPositionSeconds);
        afterRetry.ListenedSeconds.Should().Be(saved.ListenedSeconds);
        afterRetry.EndedAt.Should().Be(saved.EndedAt);
        afterRetry.PlaybackFinished.Should().Be(saved.PlaybackFinished);
    }

    [Fact]
    public async Task CloseSessionAsync_failed_history_write_rolls_back_session_and_allows_retry()
    {
        var (historyId, sessionIds) = await SeedHistoryAsync(1);
        var ct = TestContext.Current.CancellationToken;
        await using var db = Fixture.CreateDbContext();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            CREATE FUNCTION fail_stream_history_close() RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'simulated history write failure';
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER fail_stream_history_close
            BEFORE UPDATE ON "StreamHistory"
            FOR EACH ROW EXECUTE FUNCTION fail_stream_history_close();
            """, ct);

        try
        {
            await using var failedDb = Fixture.CreateDbContext();
            var repository = new StreamHistoryRepository(failedDb);
            var session = await PrepareCloseAsync(repository, sessionIds[0], 30, 600, true,
                SeedBase.AddMinutes(1));

            var close = () => repository.CloseSessionAsync(session, UserId, ct);

            await close.Should().ThrowAsync<PostgresException>()
                .WithMessage("*simulated history write failure*");

            var storedSession = await db.StreamSessions.AsNoTracking().SingleAsync(s => s.Id == sessionIds[0], ct);
            var history = await db.StreamHistories.AsNoTracking().SingleAsync(h => h.Id == historyId, ct);

            storedSession.EndedAt.Should().BeNull();
            storedSession.ListenedSeconds.Should().Be(0);
            storedSession.IsQualifiedPlay.Should().BeFalse();
            storedSession.PlaybackFinished.Should().BeFalse();
            history.TotalListenedSeconds.Should().Be(100);
            history.QualifiedPlayCount.Should().Be(0);
            history.HasFinished.Should().BeFalse();
        }
        finally
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DROP FUNCTION fail_stream_history_close() CASCADE;", ct);
        }

        await using var retryDb = Fixture.CreateDbContext();
        var retryRepository = new StreamHistoryRepository(retryDb);
        var retry = await PrepareCloseAsync(retryRepository, sessionIds[0], 30, 600, true,
            SeedBase.AddMinutes(1));
        var retriedHistory = await retryRepository.CloseSessionAsync(retry, UserId, ct);

        retriedHistory.TotalListenedSeconds.Should().Be(130);
        retriedHistory.QualifiedPlayCount.Should().Be(1);
        retriedHistory.HasFinished.Should().BeTrue();
        (await retryRepository.GetSessionByIdAsync(sessionIds[0], ct))!.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdatePositionAsync_stale_context_does_not_overwrite_closed_session_counters()
    {
        var (historyId, sessionIds) = await SeedHistoryAsync(1);
        var ct = TestContext.Current.CancellationToken;
        await using var positionDb = Fixture.CreateDbContext();
        var staleHistory = await positionDb.StreamHistories.SingleAsync(h => h.Id == historyId, ct);
        staleHistory.TotalListenedSeconds.Should().Be(100);

        await using var closeDb = Fixture.CreateDbContext();
        var closeRepository = new StreamHistoryRepository(closeDb);
        var session = await PrepareCloseAsync(closeRepository, sessionIds[0], 30, 600, true,
            SeedBase.AddMinutes(1));
        await closeRepository.CloseSessionAsync(session, UserId, ct);

        var positionRepository = new StreamHistoryRepository(positionDb);
        await positionRepository.UpdatePositionAsync(historyId, UserId, 10, ct);
        var history = await positionRepository.GetByIdAndUserIdAsync(historyId, UserId, ct);

        history.Should().NotBeNull();
        history.PositionSeconds.Should().Be(10);
        history.TotalListenedSeconds.Should().Be(130);
        history.QualifiedPlayCount.Should().Be(1);
        history.HasFinished.Should().BeTrue();
        history.MaxPositionReachedSeconds.Should().Be(600);
    }

    [Fact]
    public async Task CloseSessionAsync_foreign_owner_cannot_close_session_or_increment_history()
    {
        var (historyId, sessionIds) = await SeedHistoryAsync(1);
        var ct = TestContext.Current.CancellationToken;
        await using var db = Fixture.CreateDbContext();
        var repository = new StreamHistoryRepository(db);
        var session = await PrepareCloseAsync(repository, sessionIds[0], 30, 600, true,
            SeedBase.AddMinutes(1));

        var close = () => repository.CloseSessionAsync(session, Guid.NewGuid(), ct);

        await close.Should().ThrowAsync<StreamSessionNotFoundException>();

        var history = await repository.GetByIdAndUserIdAsync(historyId, UserId, ct);
        history!.TotalListenedSeconds.Should().Be(100);
        history.QualifiedPlayCount.Should().Be(0);
        (await repository.GetSessionByIdAsync(sessionIds[0], ct))!.EndedAt.Should().BeNull();
    }

    [Fact]
    public async Task CloseSessionAsync_endpoint_retry_returns_success_without_recounting()
    {
        var (_, sessionIds) = await SeedHistoryAsync(1);
        var ct = TestContext.Current.CancellationToken;
        var route = $"/api/stream-history/sessions/{sessionIds[0]}/close";
        var request = new CloseSessionRequest
        {
            EndPositionSeconds = 600, ListenedSeconds = 30, PlaybackFinished = true,
        };

        var first = await Auth.PatchAsJsonAsync(route, request, ct);
        var retry = await Auth.PatchAsJsonAsync(route, request, ct);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await retry.Content.ReadFromJsonAsync<StreamHistoryDto>(ct);
        history.Should().NotBeNull();
        history.TotalListenedSeconds.Should().Be(130);
        history.QualifiedPlayCount.Should().Be(1);
        history.HasFinished.Should().BeTrue();
    }
}
