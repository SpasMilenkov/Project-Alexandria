using Alexandria.Common.Repositories;
using Alexandria.Dto.Files.Streaming.Playlist;
using Alexandria.Services.Streaming;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Streaming;

public class PlaylistJobSelectionTests
{
    private static readonly Guid JobOld = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid JobNew = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid JobCurrent = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid VersionA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid VersionB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Owner = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid FileOne = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid FileTwo = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static DateTime Utc(int day, int hour = 0) =>
        new(2026, 8, day, hour, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SelectJob_current_version_wins_over_newer_job()
    {
        var candidates = new[]
        {
            new PlaylistJobCandidate(JobCurrent, VersionA, Utc(10)),
            new PlaylistJobCandidate(JobNew, VersionB, Utc(20)),
        };

        PlaylistJobSelection.SelectJob(candidates, VersionA).Should().Be(JobCurrent);
    }

    [Fact]
    public void SelectJob_without_current_match_picks_most_recent()
    {
        var candidates = new[]
        {
            new PlaylistJobCandidate(JobOld, VersionA, Utc(10)),
            new PlaylistJobCandidate(JobNew, VersionB, Utc(20)),
        };

        PlaylistJobSelection.SelectJob(candidates, Guid.NewGuid()).Should().Be(JobNew);
    }

    [Fact]
    public void SelectJob_null_current_version_picks_most_recent()
    {
        var candidates = new[]
        {
            new PlaylistJobCandidate(JobOld, VersionA, Utc(10)),
            new PlaylistJobCandidate(JobNew, VersionB, Utc(20)),
        };

        PlaylistJobSelection.SelectJob(candidates, null).Should().Be(JobNew);
    }

    [Fact]
    public void SelectJob_empty_candidates_returns_null()
    {
        PlaylistJobSelection.SelectJob([], VersionA).Should().BeNull();
    }

    [Fact]
    public void SelectJob_multiple_jobs_for_current_version_picks_most_recent()
    {
        var candidates = new[]
        {
            new PlaylistJobCandidate(JobOld, VersionA, Utc(10)),
            new PlaylistJobCandidate(JobCurrent, VersionA, Utc(15)),
            new PlaylistJobCandidate(JobNew, VersionB, Utc(20)),
        };

        PlaylistJobSelection.SelectJob(candidates, VersionA).Should().Be(JobCurrent);
    }

    [Fact]
    public void SelectJob_missing_completion_sorts_oldest_but_still_resolves()
    {
        var candidates = new[]
        {
            new PlaylistJobCandidate(JobOld, VersionA, null),
        };

        PlaylistJobSelection.SelectJob(candidates, VersionB).Should().Be(JobOld);
    }

    [Fact]
    public void SelectJob_missing_completion_loses_to_dated_job()
    {
        var candidates = new[]
        {
            new PlaylistJobCandidate(JobOld, VersionA, null),
            new PlaylistJobCandidate(JobNew, VersionB, Utc(20)),
        };

        PlaylistJobSelection.SelectJob(candidates, Guid.NewGuid()).Should().Be(JobNew);
    }

    [Fact]
    public async Task ResolveForOwnerAsync_resolves_each_file_and_skips_unresolvable()
    {
        var jobs = Substitute.For<ITranspilationJobRepository>();
        jobs.GetResolvableJobsAsync(Owner, Arg.Any<CancellationToken>())
            .Returns(new List<ResolvableTranscodeRow>
            {
                new(JobCurrent, FileOne, VersionA, Utc(10), VersionA),
                new(JobNew, FileOne, VersionB, Utc(20), VersionA),
                new(JobOld, FileTwo, VersionB, Utc(12), VersionB),
            });
        var sut = new PlaylistJobResolver(jobs);

        var result = await sut.ResolveForOwnerAsync(Owner, TestContext.Current.CancellationToken);

        await jobs.Received(1).GetResolvableJobsAsync(Owner, Arg.Any<CancellationToken>());
        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.FileId == FileOne)
            .Subject.TranspilationJobId.Should().Be(JobCurrent);
        result.Should().ContainSingle(r => r.FileId == FileTwo)
            .Subject.TranspilationJobId.Should().Be(JobOld);
    }

    [Fact]
    public async Task ResolveForOwnerAsync_no_rows_returns_empty()
    {
        var jobs = Substitute.For<ITranspilationJobRepository>();
        jobs.GetResolvableJobsAsync(Owner, Arg.Any<CancellationToken>())
            .Returns(new List<ResolvableTranscodeRow>());
        var sut = new PlaylistJobResolver(jobs);

        var result = await sut.ResolveForOwnerAsync(Owner, TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }
}