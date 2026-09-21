using System.Net;
using System.Net.Http.Json;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Streaming;

public class ShuffleSessionTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/streaming/shuffle-sessions";

    private sealed record ShuffleSourceDto(bool IsVideo, Guid? PlaylistId);

    private sealed record ShuffleItemDto(int Position, MediaFileDto File);

    private sealed record ShuffleResponseDto(
        Guid SessionId,
        ShuffleSourceDto Source,
        int AlgorithmVersion,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt,
        int TotalCount,
        int? AnchorPosition,
        int Offset,
        int ScannedCount,
        int? NextOffset,
        List<ShuffleItemDto> Items);

    private static readonly DateTime SeedBase = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    private async Task<List<StreamableFileSeed>> SeedAudioAsync(int count, double duration = 120.0)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var seeds = new List<StreamableFileSeed>(count);
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < count; i++)
            seeds.Add(await StreamingSeedHelper.SeedStreamableFileAsync(
                db, UserId, $"track-{i}.mp3", SeedBase.AddMinutes(i), duration, ct: ct));
        return seeds;
    }

    private async Task<ShuffleResponseDto> CreateSessionAsync(
        HttpClient client, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await client.PostAsJsonAsync(
            Route, body, cancellationToken: TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(expected);
        return expected == HttpStatusCode.OK
            ? (await response.Content.ReadFromJsonAsync<ShuffleResponseDto>(
                cancellationToken: TestContext.Current.CancellationToken))!
            : null!;
    }

    private async Task<ShuffleResponseDto> GetBatchAsync(
        HttpClient client, Guid sessionId, int offset, int limit)
    {
        var response = await client.GetAsync(
            $"{Route}/{sessionId}?offset={offset}&limit={limit}",
            cancellationToken: TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ShuffleResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task Library_create_traverses_full_source_in_pages()
    {
        await SeedAudioAsync(5);
        var requestId = Guid.NewGuid();

        var created = await CreateSessionAsync(Auth, new
        {
            requestId,
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 2,
        });

        created.TotalCount.Should().Be(5);
        created.Offset.Should().Be(0);
        created.ScannedCount.Should().Be(2);
        created.NextOffset.Should().Be(2);
        created.Items.Select(i => i.Position).Should().Equal(0, 1);

        var second = await GetBatchAsync(Auth, created.SessionId, 2, 2);
        second.Items.Select(i => i.Position).Should().Equal(2, 3);
        second.NextOffset.Should().Be(4);

        var third = await GetBatchAsync(Auth, created.SessionId, 4, 2);
        third.Items.Should().ContainSingle();
        third.Items[0].Position.Should().Be(4);
        third.NextOffset.Should().BeNull();

        var allIds = created.Items.Concat(second.Items).Concat(third.Items)
            .Select(i => i.File.FileId).ToList();
        allIds.Should().HaveCount(5).And.OnlyHaveUniqueItems();

        var terminal = await GetBatchAsync(Auth, created.SessionId, 5, 2);
        terminal.Items.Should().BeEmpty();
        terminal.ScannedCount.Should().Be(0);
        terminal.NextOffset.Should().BeNull();
    }

    [Fact]
    public async Task Library_same_session_returns_same_order_for_any_page_size()
    {
        await SeedAudioAsync(5);
        var created = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 50,
        });

        var full = created.Items.Select(i => i.File.FileId).ToList();
        var one = await GetBatchAsync(Auth, created.SessionId, 0, 1);
        var rest = await GetBatchAsync(Auth, created.SessionId, 1, 50);

        one.Items.Select(i => i.File.FileId)
            .Concat(rest.Items.Select(i => i.File.FileId))
            .Should().Equal(full);
    }

    [Fact]
    public async Task Library_offset_beyond_snapshot_returns_400()
    {
        await SeedAudioAsync(2);
        var created = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 50,
        });

        var response = await Auth.GetAsync(
            $"{Route}/{created.SessionId}?offset=3&limit=50",
            cancellationToken: TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_replay_returns_same_session_and_conflict_returns_409()
    {
        await SeedAudioAsync(3);
        var requestId = Guid.NewGuid();
        var body = new
        {
            requestId,
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 50,
        };

        var first = await CreateSessionAsync(Auth, body);
        var second = await CreateSessionAsync(Auth, body);
        second.SessionId.Should().Be(first.SessionId);
        second.Items.Select(i => i.File.FileId)
            .Should().Equal(first.Items.Select(i => i.File.FileId));

        var conflict = await Auth.PostAsJsonAsync(Route,
            new
            {
                requestId,
                source = new { isVideo = false, playlistId = (Guid?)null },
                limit = 10,
            }, cancellationToken: TestContext.Current.CancellationToken);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Sessions_are_owner_isolated()
    {
        await SeedAudioAsync(2);
        var created = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 50,
        });

        var (otherClient, _) = await CreateOtherUserAsync();
        var foreign = await otherClient.GetAsync(
            $"{Route}/{created.SessionId}?offset=0&limit=50",
            cancellationToken: TestContext.Current.CancellationToken);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var missing = await Auth.GetAsync(
            $"{Route}/{Guid.NewGuid()}?offset=0&limit=50",
            cancellationToken: TestContext.Current.CancellationToken);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var anonymous = await Anon.PostAsJsonAsync(Route,
            new
            {
                requestId = Guid.NewGuid(),
                source = new { isVideo = false, playlistId = (Guid?)null },
                limit = 50,
            }, cancellationToken: TestContext.Current.CancellationToken);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_releases_session()
    {
        await SeedAudioAsync(2);
        var created = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 50,
        });

        var deleted = await Auth.DeleteAsync(
            $"{Route}/{created.SessionId}",
            cancellationToken: TestContext.Current.CancellationToken);
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var gone = await Auth.GetAsync(
            $"{Route}/{created.SessionId}?offset=0&limit=50",
            cancellationToken: TestContext.Current.CancellationToken);
        gone.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var repeat = await Auth.DeleteAsync(
            $"{Route}/{created.SessionId}",
            cancellationToken: TestContext.Current.CancellationToken);
        repeat.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Empty_source_returns_valid_empty_session()
    {
        var (otherClient, _) = await CreateOtherUserAsync();
        var created = await CreateSessionAsync(otherClient, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 50,
        });

        created.TotalCount.Should().Be(0);
        created.Items.Should().BeEmpty();
        created.NextOffset.Should().BeNull();
    }

    [Fact]
    public async Task Foreign_playlist_create_returns_404()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var seed = await StreamingSeedHelper.SeedStreamableFileAsync(
            db, otherId, "other.mp3", SeedBase, 120.0, ct: ct);
        var (playlistId, _) = await StreamingSeedHelper.SeedPlaylistAsync(
            db, otherId, "other-list", [seed.JobId], ct);

        var response = await Auth.PostAsJsonAsync(Route,
            new
            {
                requestId = Guid.NewGuid(),
                source = new { isVideo = false, playlistId },
                limit = 50,
            }, cancellationToken: ct);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Playlist_shuffle_covers_distinct_files_and_sequential_keeps_duplicates()
    {
        var seeds = await SeedAudioAsync(3);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, _) = await StreamingSeedHelper.SeedPlaylistAsync(db, UserId, "mix",
            [seeds[0].JobId, seeds[1].JobId, seeds[0].JobId, seeds[2].JobId], ct);

        var shuffled = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)playlistId },
            limit = 50,
        });
        shuffled.TotalCount.Should().Be(3);
        shuffled.Items.Select(i => i.File.FileId).Should().OnlyHaveUniqueItems();

        var sequential = await Auth.GetAsync(
            $"/api/streaming/files?playlistId={playlistId}&page=1&pageSize=10&isVideo=false",
            cancellationToken: ct);
        sequential.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await sequential.Content.ReadFromJsonAsync<PaginatedResult<MediaFileDto>>(
            cancellationToken: ct);
        page!.TotalCount.Should().Be(4);
        page.Items.Select(i => i.FileId).Should()
            .Equal(seeds[0].FileId, seeds[1].FileId, seeds[0].FileId, seeds[2].FileId);
        page.Items.Select(i => i.PlaylistItemId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task New_file_is_absent_from_snapshot_and_present_in_next_cycle()
    {
        await SeedAudioAsync(3);
        var first = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 50,
        });
        first.TotalCount.Should().Be(3);

        await SeedAudioAsync(1);
        var reread = await GetBatchAsync(Auth, first.SessionId, 0, 50);
        reread.TotalCount.Should().Be(3);
        reread.Items.Should().HaveCount(3);

        var second = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 50,
        });
        second.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task Deleted_mid_cycle_entries_are_omitted_with_continuation_preserved()
    {
        var seeds = await SeedAudioAsync(3);
        var created = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 50,
        });
        created.TotalCount.Should().Be(3);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var doomed = await db.Files.FindAsync(seeds[1].FileId, ct);
        doomed!.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var reread = await GetBatchAsync(Auth, created.SessionId, 0, 50);
        reread.TotalCount.Should().Be(3);
        reread.Items.Should().HaveCount(2);
        reread.ScannedCount.Should().Be(3);
        reread.NextOffset.Should().BeNull();
        reread.Items.Select(i => i.File.FileId).Should().NotContain(seeds[1].FileId);
    }

    [Fact]
    public async Task Fallback_version_file_is_included_with_a_viable_job()
    {
        var seeds = await SeedAudioAsync(1);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var ct = TestContext.Current.CancellationToken;

        var file = await db.Files.FindAsync(seeds[0].FileId, ct);
        var co = new ContentObjectBuilder().Build();
        var second = new FileVersionBuilder()
            .WithContentObject(co)
            .WithCreatedBy(UserId)
            .WithMimeType("audio/mpeg")
            .Build();
        second.FileId = file!.Id;
        second.File = file;
        db.ContentObjects.Add(co);
        db.FileVersions.Add(second);
        await db.SaveChangesAsync(ct);
        file.CurrentVersionId = second.Id;
        await db.SaveChangesAsync(ct);

        var created = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 50,
        });

        created.TotalCount.Should().Be(1);
        created.Items.Should().ContainSingle();
        created.Items[0].File.TranspilationJobId.Should().Be(seeds[0].JobId);
        created.Items[0].File.PlaybackVersionId.Should().Be(seeds[0].VersionId);
        created.Items[0].File.PlaybackVersionId.Should().NotBe(created.Items[0].File.CurrentVersionId);
    }

    [Fact]
    public async Task Playlist_hydration_keeps_the_exact_stored_job_binding()
    {
        var seeds = await SeedAudioAsync(1);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;

        var file = await db.Files.FindAsync(seeds[0].FileId, ct);
        var co = new ContentObjectBuilder().Build();
        var second = new FileVersionBuilder()
            .WithContentObject(co)
            .WithCreatedBy(UserId)
            .WithMimeType("audio/mpeg")
            .Build();
        second.FileId = file!.Id;
        second.File = file;
        db.ContentObjects.Add(co);
        db.FileVersions.Add(second);
        var newerJob = new JobBuilder()
            .WithStatus(JobStatus.Ready)
            .WithType(JobType.Transpilation)
            .WithUser(UserId)
            .WithCompletedAt(now)
            .Build();
        db.Set<Job>().Add(newerJob);
        await db.SaveChangesAsync(ct);
        var newerTranscode = new TranspilationJob
        {
            Id = Guid.NewGuid(),
            VersionId = second.Id,
            JobId = newerJob.Id,
            IsVideo = false,
            UserId = UserId,
            SegmentPrefix = $"seg-{file.Id:N}-v2",
            CreatedAt = now,
        };
        db.Set<TranspilationJob>().Add(newerTranscode);
        db.Set<StreamingRepresentation>().Add(new StreamingRepresentation
        {
            Id = Guid.NewGuid(),
            TranspilationId = newerTranscode.Id,
            Codec = StreamCodec.Opus,
            Status = RepresentationStatus.Ready,
            Size = 1024,
            CreatedAt = now,
        });
        file.CurrentVersionId = second.Id;
        await db.SaveChangesAsync(ct);

        var (playlistId, _) = await StreamingSeedHelper.SeedPlaylistAsync(
            db, UserId, "binding", [seeds[0].JobId], ct);

        var shuffled = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)playlistId },
            limit = 50,
        });

        shuffled.TotalCount.Should().Be(1);
        shuffled.Items.Should().ContainSingle();
        shuffled.Items[0].File.TranspilationJobId.Should().Be(seeds[0].JobId);
    }

    [Fact]
    public async Task Listening_history_does_not_change_cycle_membership()
    {
        var seeds = await SeedAudioAsync(3);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var ct = TestContext.Current.CancellationToken;
        await StreamingSeedHelper.SeedClosedSessionAsync(
            db, UserId, seeds[0].FileId, 60, SeedBase.AddDays(-1), ct);
        await StreamingSeedHelper.SeedClosedSessionAsync(
            db, UserId, seeds[1].FileId, 5, SeedBase.AddDays(-1), ct);

        var created = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 50,
        });
        created.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Video_source_is_separate_from_audio()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var ct = TestContext.Current.CancellationToken;
        await StreamingSeedHelper.SeedStreamableFileAsync(
            db, UserId, "clip.mp4", SeedBase, 300.0, isVideo: true, ct: ct);
        await SeedAudioAsync(2);

        var audio = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)null },
            limit = 50,
        });
        audio.TotalCount.Should().Be(2);

        var video = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = true, playlistId = (Guid?)null },
            limit = 50,
        });
        video.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Anchored_create_starts_at_anchor_and_missing_anchor_conflicts()
    {
        var seeds = await SeedAudioAsync(4);

        var anchored = await CreateSessionAsync(Auth, new
        {
            requestId = Guid.NewGuid(),
            source = new { isVideo = false, playlistId = (Guid?)null },
            anchorFileId = seeds[2].FileId,
            limit = 50,
        });
        anchored.AnchorPosition.Should().Be(0);
        anchored.Items[0].File.FileId.Should().Be(seeds[2].FileId);
        anchored.TotalCount.Should().Be(4);
        anchored.Items.Select(i => i.File.FileId).Should().OnlyHaveUniqueItems();

        var missing = await Auth.PostAsJsonAsync(Route,
            new
            {
                requestId = Guid.NewGuid(),
                source = new { isVideo = false, playlistId = (Guid?)null },
                anchorFileId = Guid.NewGuid(),
                limit = 50,
            }, cancellationToken: TestContext.Current.CancellationToken);
        missing.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Invalid_shuffle_requests_are_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var anchorPlusAvoid = await Auth.PostAsJsonAsync(Route,
            new
            {
                requestId = Guid.NewGuid(),
                source = new { isVideo = false, playlistId = (Guid?)null },
                anchorFileId = Guid.NewGuid(),
                avoidFirstFileId = Guid.NewGuid(),
                limit = 50,
            }, cancellationToken: ct);
        anchorPlusAvoid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var itemWithoutPlaylist = await Auth.PostAsJsonAsync(Route,
            new
            {
                requestId = Guid.NewGuid(),
                source = new { isVideo = false, playlistId = (Guid?)null },
                anchorFileId = Guid.NewGuid(),
                anchorPlaylistItemId = Guid.NewGuid(),
                limit = 50,
            }, cancellationToken: ct);
        itemWithoutPlaylist.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var badLimit = await Auth.PostAsJsonAsync(Route,
            new
            {
                requestId = Guid.NewGuid(),
                source = new { isVideo = false, playlistId = (Guid?)null },
                limit = 0,
            }, cancellationToken: ct);
        badLimit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}