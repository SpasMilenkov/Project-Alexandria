using System.Net;
using System.Net.Http.Json;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;
using Alexandria.Repositories;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Streaming;

public class JumpBackInTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/stats/jump-back-in";

    private static DateTime Today => DateOnly.FromDateTime(DateTime.UtcNow)
        .ToDateTime(new TimeOnly(0, 0), DateTimeKind.Utc);

    private static StreamHistory History(Guid userId, Guid fileId, int plays, DateTime accessedAt) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, FileId = fileId,
        QualifiedPlayCount = plays, TotalListenedSeconds = plays * 120L,
        LastPlayedAt = Today.AddDays(-40), LastAccessedAt = accessedAt, CreatedAt = Today.AddDays(-100)
    };

    [Fact]
    public async Task GetJumpBackIn_ranks_only_inactive_familiar_playable_owned_audio_before_limit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (otherClient, otherUserId) = await CreateOtherUserAsync();
        using var other = otherClient;
        await using var db = Fixture.CreateDbContext();
        var seeds = new List<StreamableFileSeed>();

        for (var index = 0; index < 6; index++)
            seeds.Add(await StreamingSeedHelper.SeedStreamableFileAsync(
                db, UserId, $"jump-{index}.mp3", Today.AddDays(-100), 120, ct: ct));

        var foreign = await StreamingSeedHelper.SeedStreamableFileAsync(
            db, otherUserId, "foreign.mp3", Today.AddDays(-100), 120, ct: ct);
        var video = await StreamingSeedHelper.SeedStreamableFileAsync(
            db, UserId, "film.mp4", Today.AddDays(-100), 120, isVideo: true, ct: ct);
        var notReady = await db.TranspilationJobs.Include(j => j.Job).SingleAsync(j => j.Id == seeds[5].JobId, ct);
        notReady.Job.Status = JobStatus.Failed;

        db.StreamHistories.AddRange(
            History(UserId, seeds[0].FileId, 5, Today.AddDays(-40)),
            History(UserId, seeds[1].FileId, 3, Today.AddDays(-40)),
            History(UserId, seeds[2].FileId, 2, Today.AddDays(-40)),
            History(UserId, seeds[3].FileId, 100, Today.AddDays(-10)),
            History(UserId, seeds[4].FileId, 1, Today.AddDays(-40)),
            History(UserId, seeds[5].FileId, 1000, Today.AddDays(-40)),
            History(otherUserId, foreign.FileId, 2000, Today.AddDays(-40)),
            History(UserId, foreign.FileId, 2000, Today.AddDays(-40)),
            History(UserId, video.FileId, 2000, Today.AddDays(-40)));
        await db.SaveChangesAsync(ct);

        var response = await Auth.GetAsync($"{Route}?limit=2", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<JumpBackInResponse>(cancellationToken: ct))!;

        body.InactiveDays.Should().Be(30);
        body.InactiveBefore.Kind.Should().Be(DateTimeKind.Utc);
        body.Items.Select(s => s.File.FileId).Should().Equal(seeds[0].FileId, seeds[1].FileId);
        body.Items.Select(s => s.QualifiedPlayCount).Should().Equal(5, 3);
        body.Items.Select(s => s.File.PlaybackVersionId).Should().Equal(seeds[0].VersionId, seeds[1].VersionId);
        body.Items.Select(s => s.File.TranspilationJobId).Should().Equal(seeds[0].JobId, seeds[1].JobId);

        var longerGap = await Auth.GetAsync($"{Route}?inactiveDays=60", ct);
        longerGap.StatusCode.Should().Be(HttpStatusCode.OK);
        var longerBody = (await longerGap.Content.ReadFromJsonAsync<JumpBackInResponse>(cancellationToken: ct))!;
        longerBody.InactiveDays.Should().Be(60);
        longerBody.Items.Should().BeEmpty();

        var foreignResponse = await other.GetAsync(Route, ct);
        foreignResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var foreignBody = (await foreignResponse.Content.ReadFromJsonAsync<JumpBackInResponse>(cancellationToken: ct))!;
        foreignBody.Items.Should().ContainSingle().Which.File.FileId.Should().Be(foreign.FileId);
    }

    [Fact]
    public async Task JumpBackInQuery_with_postgres_excludes_exact_access_cutoff_and_accepts_older_access()
    {
        var ct = TestContext.Current.CancellationToken;
        var cutoff = Today.AddDays(-30);
        await using var db = Fixture.CreateDbContext();
        var exact = await StreamingSeedHelper.SeedStreamableFileAsync(
            db, UserId, "exact.mp3", Today.AddDays(-100), 120, ct: ct);
        var older = await StreamingSeedHelper.SeedStreamableFileAsync(
            db, UserId, "older.mp3", Today.AddDays(-100), 120, ct: ct);
        db.StreamHistories.AddRange(
            History(UserId, exact.FileId, 100, cutoff),
            History(UserId, older.FileId, 2, cutoff.AddSeconds(-1)));
        await db.SaveChangesAsync(ct);

        var result = await new StreamHistoryRepository(db).GetJumpBackInAsync(UserId, cutoff, 1, ct);

        result.Should().ContainSingle().Which.FileId.Should().Be(older.FileId);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=21")]
    [InlineData("inactiveDays=6")]
    [InlineData("inactiveDays=366")]
    public async Task GetJumpBackIn_invalid_bounds_returns_validation_error(string query)
    {
        var response = await Auth.GetAsync($"{Route}?{query}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetJumpBackIn_anonymous_returns_unauthorized()
    {
        var response = await Anon.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
