using Alexandria.Common.Policies;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Services.Streaming.Backfill;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class TranscodeBackfillPlannerTests
{
    private static readonly TranscodeParameters VideoRule = new(
        AudioRungs: [], VideoRungs: [VideoRung.P720, VideoRung.P1080], GenerateThumbnail: true);

    private static readonly TranscodeParameters AudioRule = new(
        AudioRungs: [AudioRung.Kbps192, AudioRung.Kbps320], VideoRungs: [], GenerateThumbnail: true);

    private static ExistingTranscode Video(JobStatus status, params VideoRung[] rungs) => new()
    {
        TranspilationId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        IsVideo = true,
        Status = status,
        AudioRungs = [],
        VideoRungs = rungs,
    };

    private static ExistingTranscode Audio(JobStatus status, params AudioRung[] rungs) => new()
    {
        TranspilationId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        IsVideo = false,
        Status = status,
        AudioRungs = rungs,
        VideoRungs = [],
    };

    [Fact]
    public void Plan_neverTranscoded_createsAJob()
    {
        TranscodeBackfillPlanner.Plan(VideoRule, null).Action.Should().Be(TranscodeBackfillAction.Create);
    }

    [Theory]
    [InlineData(JobStatus.Queued)]
    [InlineData(JobStatus.Processing)]
    [InlineData(JobStatus.CancellationRequested)]
    public void Plan_transcodeStillRunning_leavesItAlone(JobStatus status)
    {
        TranscodeBackfillPlanner.Plan(VideoRule, Video(status)).Action.Should().Be(TranscodeBackfillAction.InFlight);
    }

    [Fact]
    public void Plan_cancelledByTheOwner_staysCancelled()
    {
        TranscodeBackfillPlanner.Plan(VideoRule, Video(JobStatus.Cancelled, VideoRung.P360))
            .Action.Should().Be(TranscodeBackfillAction.Cancelled);
    }

    [Fact]
    public void Plan_readyWithEveryRuleRung_isCovered()
    {
        var existing = Video(JobStatus.Ready, VideoRung.P360, VideoRung.P720, VideoRung.P1080);

        TranscodeBackfillPlanner.Plan(VideoRule, existing).Action.Should().Be(TranscodeBackfillAction.Covered);
    }

    [Fact]
    public void Plan_readyButMissingARung_requeuesWithTheUnionAndKeepsAudio()
    {
        var step = TranscodeBackfillPlanner.Plan(VideoRule, Video(JobStatus.Ready, VideoRung.P360, VideoRung.P720));

        step.Action.Should().Be(TranscodeBackfillAction.Requeue);
        step.VideoRungs.Should().Equal(VideoRung.P360, VideoRung.P720, VideoRung.P1080);
        step.AudioRungs.Should().BeNull();
    }

    [Theory]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.Partial)]
    public void Plan_unfinishedWithEveryRuleRung_isLeftAlone(JobStatus status)
    {
        var step = TranscodeBackfillPlanner.Plan(AudioRule, Audio(status, AudioRung.Kbps192, AudioRung.Kbps320));

        step.Action.Should().Be(TranscodeBackfillAction.Unfinished);
    }

    [Theory]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.Partial)]
    public void Plan_unfinishedButMissingARung_requeuesWithTheUnion(JobStatus status)
    {
        var step = TranscodeBackfillPlanner.Plan(AudioRule, Audio(status, AudioRung.Kbps128, AudioRung.Kbps192));

        step.Action.Should().Be(TranscodeBackfillAction.Requeue);
        step.AudioRungs.Should().Equal(AudioRung.Kbps128, AudioRung.Kbps192, AudioRung.Kbps320);
        step.VideoRungs.Should().BeNull();
    }

    [Fact]
    public void Plan_audioRuleOverAReadyVideo_isCovered()
    {
        TranscodeBackfillPlanner.Plan(AudioRule, Video(JobStatus.Ready, VideoRung.P1080))
            .Action.Should().Be(TranscodeBackfillAction.Covered);
    }
}
