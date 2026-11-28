using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Policies;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Services.Streaming.Backfill;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Tests.Unit.Streaming;

public class TranscodeBackfillServiceTests
{
    private static readonly Guid PolicyId = Guid.NewGuid();
    private static readonly Guid RuleId = Guid.NewGuid();
    private static readonly Guid DirectoryId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTimeOffset Cutoff = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly TranscodeParameters RuleParameters = new(
        AudioRungs: [], VideoRungs: [VideoRung.P720, VideoRung.P1080], GenerateThumbnail: true);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDirectoryPolicyRepository _policies = Substitute.For<IDirectoryPolicyRepository>();
    private readonly IDirectoryRepository _directories = Substitute.For<IDirectoryRepository>();
    private readonly ITranspilationJobRepository _transcodes = Substitute.For<ITranspilationJobRepository>();
    private readonly ITranspilationJobService _jobService = Substitute.For<ITranspilationJobService>();

    private readonly TranscodeBackfillRequest _request = new()
    {
        PolicyId = PolicyId,
        RuleId = RuleId,
        Cutoff = Cutoff,
    };

    public TranscodeBackfillServiceTests()
    {
        _unitOfWork.DirectoryPolicies.Returns(_policies);
        _unitOfWork.Directories.Returns(_directories);
        _unitOfWork.TranspilationJobs.Returns(_transcodes);
    }

    private TranscodeBackfillService CreateSut(bool streaming = true)
    {
        return new TranscodeBackfillService(_unitOfWork, _jobService,
            Substitute.For<ILogger<TranscodeBackfillService>>(),
            Options.Create(new ProcessingCapabilities { AdaptiveStreaming = streaming }));
    }

    private void GivenPolicy(bool inherited = false, PolicyActionType actionType = PolicyActionType.Transcode,
        bool anyFile = false)
    {
        var triggerType = PolicyTriggerType.FileGroup;
        var triggerValue = FileGroups.Videos;

        if (anyFile)
        {
            triggerType = PolicyTriggerType.AnyFile;
            triggerValue = "*";
        }

        _policies.GetByIdAsync(PolicyId, Arg.Any<CancellationToken>()).Returns(new DirectoryPolicy
        {
            Id = PolicyId,
            DirectoryId = DirectoryId,
            InheritedByChildren = inherited,
            Rules =
            [
                new PolicyRule
                {
                    Id = RuleId,
                    PolicyId = PolicyId,
                    ActionType = actionType,
                    TriggerType = triggerType,
                    TriggerValue = triggerValue,
                    Parameters = JsonSerializer.SerializeToDocument(RuleParameters, JsonOptions),
                },
            ],
        });
    }

    private void GivenCandidates(params TranscodeBackfillCandidate[] candidates)
    {
        _transcodes.GetTranscodeBackfillCandidatesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(candidates);
    }

    private static TranscodeBackfillCandidate Candidate(string mimeType = "video/mp4", ExistingTranscode? transcode = null)
        => new()
        {
            FileId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            OwnerId = OwnerId,
            MimeType = mimeType,
            Transcode = transcode,
        };

    private static ExistingTranscode ReadyVideo(params VideoRung[] rungs) => new()
    {
        TranspilationId = Guid.NewGuid(),
        UserId = OwnerId,
        IsVideo = true,
        Status = JobStatus.Ready,
        AudioRungs = [],
        VideoRungs = rungs,
    };

    [Fact]
    public async Task BackfillRuleAsync_streamingDisabled_doesNothing()
    {
        var result = await CreateSut(streaming: false).BackfillRuleAsync(_request, TestContext.Current.CancellationToken);

        result.Candidates.Should().Be(0);
        await _policies.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task BackfillRuleAsync_ruleIsNotTranscode_doesNothing()
    {
        GivenPolicy(actionType: PolicyActionType.AutoTag);

        var result = await CreateSut().BackfillRuleAsync(_request, TestContext.Current.CancellationToken);

        result.Candidates.Should().Be(0);
        await _transcodes.DidNotReceiveWithAnyArgs()
            .GetTranscodeBackfillCandidatesAsync(default!, default, default);
    }

    [Fact]
    public async Task BackfillRuleAsync_mixedLibrary_createsRequeuesAndSkipsPerFile()
    {
        GivenPolicy();

        var untranscoded = Candidate();
        var missingRung = Candidate(transcode: ReadyVideo(VideoRung.P720));
        var covered = Candidate(transcode: ReadyVideo(VideoRung.P720, VideoRung.P1080));
        var audio = Candidate(mimeType: "audio/mpeg");
        GivenCandidates(untranscoded, missingRung, covered, audio);

        var result = await CreateSut().BackfillRuleAsync(_request, TestContext.Current.CancellationToken);

        result.Should().Be(new TranscodeBackfillResult { Candidates = 4, Created = 1, Requeued = 1, Skipped = 2 });

        await _jobService.Received(1).CreateJobAsync(untranscoded.VersionId, OwnerId,
            Arg.Is<AudioRung[]>(r => r.Length == 0),
            Arg.Is<VideoRung[]>(r => r.SequenceEqual(RuleParameters.VideoRungs)),
            Arg.Any<CancellationToken>());

        await _jobService.Received(1).UpdateStatusAsync(missingRung.Transcode!.TranspilationId, OwnerId,
            JobStatus.Queued, Arg.Is<AudioRung[]?>(r => r == null),
            Arg.Is<VideoRung[]?>(r => r != null && r.SequenceEqual(new[] { VideoRung.P720, VideoRung.P1080 })),
            Arg.Any<CancellationToken>());

        await _jobService.Received(1).CreateJobAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<AudioRung[]>(), Arg.Any<VideoRung[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillRuleAsync_anyFileRule_onlyQueuesAudioAndVideo()
    {
        GivenPolicy(anyFile: true);

        var video = Candidate();
        var audio = Candidate(mimeType: "audio/mpeg");
        var document = Candidate(mimeType: "application/pdf");
        var image = Candidate(mimeType: "image/jpeg");
        GivenCandidates(video, audio, document, image);

        var result = await CreateSut().BackfillRuleAsync(_request, TestContext.Current.CancellationToken);

        result.Should().Be(new TranscodeBackfillResult { Candidates = 4, Created = 2, Requeued = 0, Skipped = 2 });

        await _jobService.DidNotReceive().CreateJobAsync(document.VersionId, Arg.Any<Guid>(),
            Arg.Any<AudioRung[]>(), Arg.Any<VideoRung[]>(), Arg.Any<CancellationToken>());
        await _jobService.DidNotReceive().CreateJobAsync(image.VersionId, Arg.Any<Guid>(),
            Arg.Any<AudioRung[]>(), Arg.Any<VideoRung[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillRuleAsync_fileQueuedDuringTheSweep_isSkippedAndTheRestContinue()
    {
        GivenPolicy();

        var raced = Candidate();
        var next = Candidate();
        GivenCandidates(raced, next);

        _jobService.CreateJobAsync(raced.VersionId, Arg.Any<Guid>(), Arg.Any<AudioRung[]>(), Arg.Any<VideoRung[]>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new TranspilationJobConflictException(raced.VersionId, Guid.NewGuid()));

        var result = await CreateSut().BackfillRuleAsync(_request, TestContext.Current.CancellationToken);

        result.Created.Should().Be(1);
        result.Skipped.Should().Be(1);
    }

    [Fact]
    public async Task BackfillRuleAsync_contentGoneDuringTheSweep_isSkippedAndTheRestContinue()
    {
        GivenPolicy();

        var orphaned = Candidate();
        var next = Candidate();
        GivenCandidates(orphaned, next);

        _jobService.CreateJobAsync(orphaned.VersionId, Arg.Any<Guid>(), Arg.Any<AudioRung[]>(),
                Arg.Any<VideoRung[]>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ContentObjectNotFoundException(orphaned.VersionId));

        var result = await CreateSut().BackfillRuleAsync(_request, TestContext.Current.CancellationToken);

        result.Created.Should().Be(1);
        result.Skipped.Should().Be(1);
        await _jobService.Received(1).CreateJobAsync(next.VersionId, Arg.Any<Guid>(), Arg.Any<AudioRung[]>(),
            Arg.Any<VideoRung[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillRuleAsync_failedJobWithEveryRung_isNotRequeued()
    {
        GivenPolicy();

        var failed = Candidate(transcode: ReadyVideo(VideoRung.P720, VideoRung.P1080) with
        {
            Status = JobStatus.Failed,
        });
        GivenCandidates(failed);

        var result = await CreateSut().BackfillRuleAsync(_request, TestContext.Current.CancellationToken);

        result.Requeued.Should().Be(0);
        await _jobService.DidNotReceiveWithAnyArgs().UpdateStatusAsync(default, default, default,
            default, default, default);
    }

    [Fact]
    public async Task BackfillRuleAsync_inheritedPolicy_leavesSubfoldersWithTheirOwnRulesOut()
    {
        GivenPolicy(inherited: true);
        GivenCandidates();

        var plain = new Directory { Id = Guid.NewGuid(), Name = "plain", ParentId = DirectoryId };
        var overridden = new Directory { Id = Guid.NewGuid(), Name = "overridden", ParentId = DirectoryId };

        var emptied = new Directory { Id = Guid.NewGuid(), Name = "emptied", ParentId = DirectoryId };

        _directories.GetAllSubDirectoriesAsync(DirectoryId, Arg.Any<CancellationToken>())
            .Returns([plain, overridden, emptied]);
        _policies.GetWithRulesByDirectoryIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                new DirectoryPolicy
                {
                    DirectoryId = overridden.Id,
                    InheritedByChildren = true,
                    Rules = [new PolicyRule { Id = Guid.NewGuid() }],
                },
                new DirectoryPolicy { DirectoryId = emptied.Id, InheritedByChildren = true },
            ]);

        await CreateSut().BackfillRuleAsync(_request, TestContext.Current.CancellationToken);

        await _transcodes.Received(1).GetTranscodeBackfillCandidatesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 3 && ids.Contains(DirectoryId)
                && ids.Contains(plain.Id) && ids.Contains(emptied.Id)),
            Cutoff.UtcDateTime,
            Arg.Any<CancellationToken>());
    }
}
