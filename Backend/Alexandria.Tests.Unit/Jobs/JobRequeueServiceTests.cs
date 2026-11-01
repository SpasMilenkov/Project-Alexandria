using System.Linq.Expressions;
using System.Text;
using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Jobs;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Jobs;

public class JobRequeueServiceTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly ITrackLyricsRepository _lyrics = Substitute.For<ITrackLyricsRepository>();
    private readonly IEssentiaBatchFileRepository _batchFiles = Substitute.For<IEssentiaBatchFileRepository>();
    private readonly IPreviewService _previews = Substitute.For<IPreviewService>();
    private readonly IPublisherService _publisher = Substitute.For<IPublisherService>();

    public JobRequeueServiceTests()
    {
        _unitOfWork.Jobs.Returns(_jobs);
        _unitOfWork.Lyrics.Returns(_lyrics);
        _unitOfWork.EssentiaBatchFiles.Returns(_batchFiles);
    }

    private JobRequeueService CreateSut() =>
        new(_unitOfWork, _previews, _publisher, Options.Create(new JobRecoveryOptions()));

    private Job GivenJob(JobType type, JobStatus status = JobStatus.Failed)
    {
        var job = new Job { Id = Guid.NewGuid(), Type = type, Status = status };
        _jobs.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        return job;
    }

    [Fact]
    public async Task requeue_unknown_job_returns_not_found()
    {
        var outcome = await CreateSut().RequeueAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        outcome.Should().Be(JobRequeueOutcome.NotFound);
    }

    [Fact]
    public async Task requeue_preview_resets_then_republishes()
    {
        var job = GivenJob(JobType.MediaPreview);
        _jobs.TryResetForManualRequeueAsync(job.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        _previews.RepublishJobAsync(job.Id, Arg.Any<CancellationToken>()).Returns(true);

        var outcome = await CreateSut().RequeueAsync(job.Id, TestContext.Current.CancellationToken);

        outcome.Should().Be(JobRequeueOutcome.Requeued);
        Received.InOrder(() =>
        {
            _jobs.TryResetForManualRequeueAsync(job.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
            _previews.RepublishJobAsync(job.Id, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task requeue_ineligible_job_is_rejected_without_publishing()
    {
        var job = GivenJob(JobType.Transpilation, JobStatus.Processing);
        _jobs.TryResetForManualRequeueAsync(job.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(false);

        var outcome = await CreateSut().RequeueAsync(job.Id, TestContext.Current.CancellationToken);

        outcome.Should().Be(JobRequeueOutcome.NotEligible);
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!);
    }

    [Fact]
    public async Task requeue_transpilation_publishes_job_id()
    {
        var job = GivenJob(JobType.Transpilation);
        _jobs.TryResetForManualRequeueAsync(job.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);

        var outcome = await CreateSut().RequeueAsync(job.Id, TestContext.Current.CancellationToken);

        outcome.Should().Be(JobRequeueOutcome.Requeued);
        await _publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == job.Id.ToString()),
            JobRequeueService.TranspilationRoutingKey);
    }

    [Fact]
    public async Task requeue_manual_lyrics_is_not_eligible()
    {
        var job = GivenJob(JobType.LyricsFetch);
        _lyrics.GetByJobIdAsync(job.Id, Arg.Any<CancellationToken>())
            .Returns(new TrackLyrics { JobId = job.Id, SourceProvider = LyricsProvider.Manual });

        var outcome = await CreateSut().RequeueAsync(job.Id, TestContext.Current.CancellationToken);

        outcome.Should().Be(JobRequeueOutcome.NotEligible);
        await _jobs.DidNotReceiveWithAnyArgs().TryResetForManualRequeueAsync(default, default, default);
    }

    [Fact]
    public async Task requeue_lyrics_marks_record_pending_and_publishes()
    {
        var job = GivenJob(JobType.LyricsFetch);
        var record = new TrackLyrics { JobId = job.Id, Status = LyricsStatus.FetchFailed };
        _lyrics.GetByJobIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(record);
        _jobs.TryResetForManualRequeueAsync(job.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);

        var outcome = await CreateSut().RequeueAsync(job.Id, TestContext.Current.CancellationToken);

        outcome.Should().Be(JobRequeueOutcome.Requeued);
        record.Status.Should().Be(LyricsStatus.PendingFetch);
        _lyrics.DidNotReceiveWithAnyArgs().Update(default!);
        await _publisher.Received(1).PublishAsync(Arg.Any<byte[]>(), JobRequeueService.LyricsRoutingKey);
    }

    [Fact]
    public async Task requeue_enrichment_triggers_file_and_retires_old_attempt()
    {
        var job = GivenJob(JobType.MetadataEnrichment);
        var fileId = Guid.NewGuid();
        _batchFiles.FirstOrDefaultAsync(Arg.Any<Expression<Func<EssentiaBatchFile, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(new EssentiaBatchFile { FileId = fileId, JobId = job.Id });

        var outcome = await CreateSut().RequeueAsync(job.Id, TestContext.Current.CancellationToken);

        outcome.Should().Be(JobRequeueOutcome.Requeued);
        await _publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == fileId.ToString()),
            JobRequeueService.EnrichRoutingKey);
        await _jobs.Received(1).UpdateStatusAsync(job.Id, JobStatus.Cancelled, Arg.Any<int?>(),
            JobRequeueService.SupersededError, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task requeue_playlist_sync_is_unsupported()
    {
        var job = GivenJob(JobType.PlaylistSync);

        var outcome = await CreateSut().RequeueAsync(job.Id, TestContext.Current.CancellationToken);

        outcome.Should().Be(JobRequeueOutcome.Unsupported);
    }
}
