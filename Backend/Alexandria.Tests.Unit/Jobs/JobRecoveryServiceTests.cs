using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Jobs;

public class JobRecoveryServiceTests
{
    private static readonly DateTime Now =
        DateTime.SpecifyKind(DateOnly.FromDateTime(DateTime.UtcNow).ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Utc);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly IPreviewService _previews = Substitute.For<IPreviewService>();

    private readonly JobRecoveryOptions _options = new()
    {
        PreviewStalledMinutes = 30,
        LyricsStalledMinutes = 15,
        TranspilationStalledMinutes = 360,
        PreviewRetryBaseDelayMinutes = 5,
        OrphanedQueuedMinutes = 30,
        BatchSize = 50
    };

    public JobRecoveryServiceTests()
    {
        _unitOfWork.Jobs.Returns(_jobs);
        _jobs.GetRetryCandidatesAsync(Arg.Any<IReadOnlyCollection<JobType>>(), Arg.Any<DateTime>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([]);
        _jobs.GetOrphanedQueuedJobsAsync(Arg.Any<IReadOnlyCollection<JobType>>(), Arg.Any<DateTime>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private JobRecoveryService CreateSut() =>
        new(_unitOfWork, _previews, Options.Create(_options), NullLogger<JobRecoveryService>.Instance);

    private static Job FailedJob(int retryCount, DateTime completedAt) => new()
    {
        Id = Guid.NewGuid(),
        Type = JobType.MediaPreview,
        Status = JobStatus.Failed,
        RetryCount = retryCount,
        CompletedAt = completedAt
    };

    [Fact]
    public async Task sweep_fails_stalled_jobs_with_per_type_thresholds()
    {
        await CreateSut().SweepAsync(Now, TestContext.Current.CancellationToken);

        await _jobs.Received(1).FailStalledJobsAsync(JobType.MediaPreview, Now.AddMinutes(-30),
            JobRecoveryService.StalledError, Arg.Any<CancellationToken>());
        await _jobs.Received(1).FailStalledJobsAsync(JobType.DocumentPreview, Now.AddMinutes(-30),
            JobRecoveryService.StalledError, Arg.Any<CancellationToken>());
        await _jobs.Received(1).FailStalledJobsAsync(JobType.LyricsFetch, Now.AddMinutes(-15),
            JobRecoveryService.StalledError, Arg.Any<CancellationToken>());
        await _jobs.Received(1).FailStalledJobsAsync(JobType.Transpilation, Now.AddMinutes(-360),
            JobRecoveryService.StalledError, Arg.Any<CancellationToken>());
        await _jobs.DidNotReceive().FailStalledJobsAsync(JobType.MetadataEnrichment, Arg.Any<DateTime>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task retry_skips_jobs_whose_backoff_has_not_elapsed()
    {
        // Third retry waits 5 * 2^2 = 20 minutes; only 10 have passed.
        var job = FailedJob(retryCount: 2, completedAt: Now.AddMinutes(-10));
        _jobs.GetRetryCandidatesAsync(Arg.Any<IReadOnlyCollection<JobType>>(), Arg.Any<DateTime>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([job]);

        await CreateSut().RetryFailedPreviewsAsync(Now, TestContext.Current.CancellationToken);

        await _jobs.DidNotReceive().TryRequeueForRetryAsync(job.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task retry_requeues_and_republishes_eligible_jobs()
    {
        var job = FailedJob(retryCount: 0, completedAt: Now.AddMinutes(-6));
        _jobs.GetRetryCandidatesAsync(Arg.Any<IReadOnlyCollection<JobType>>(), Arg.Any<DateTime>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([job]);
        _jobs.TryRequeueForRetryAsync(job.Id, Arg.Any<CancellationToken>()).Returns(true);
        _previews.RepublishJobAsync(job.Id, Arg.Any<CancellationToken>()).Returns(true);

        await CreateSut().RetryFailedPreviewsAsync(Now, TestContext.Current.CancellationToken);

        await _previews.Received(1).RepublishJobAsync(job.Id, Arg.Any<CancellationToken>());
        await _jobs.DidNotReceive().MarkNonRetryableAsync(job.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task retry_does_not_publish_when_another_sweep_won_the_requeue()
    {
        var job = FailedJob(retryCount: 0, completedAt: Now.AddMinutes(-6));
        _jobs.GetRetryCandidatesAsync(Arg.Any<IReadOnlyCollection<JobType>>(), Arg.Any<DateTime>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([job]);
        _jobs.TryRequeueForRetryAsync(job.Id, Arg.Any<CancellationToken>()).Returns(false);

        await CreateSut().RetryFailedPreviewsAsync(Now, TestContext.Current.CancellationToken);

        await _previews.DidNotReceive().RepublishJobAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task retry_marks_job_non_retryable_when_source_is_gone()
    {
        var job = FailedJob(retryCount: 0, completedAt: Now.AddMinutes(-6));
        _jobs.GetRetryCandidatesAsync(Arg.Any<IReadOnlyCollection<JobType>>(), Arg.Any<DateTime>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([job]);
        _jobs.TryRequeueForRetryAsync(job.Id, Arg.Any<CancellationToken>()).Returns(true);
        _previews.RepublishJobAsync(job.Id, Arg.Any<CancellationToken>()).Returns(false);

        await CreateSut().RetryFailedPreviewsAsync(Now, TestContext.Current.CancellationToken);

        await _jobs.Received(1).UpdateStatusAsync(job.Id, JobStatus.Failed, Arg.Any<int?>(),
            JobRecoveryService.SourceGoneError, Arg.Any<CancellationToken>());
        await _jobs.Received(1).MarkNonRetryableAsync(job.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task retry_publish_failure_returns_job_to_failed()
    {
        var job = FailedJob(retryCount: 0, completedAt: Now.AddMinutes(-6));
        _jobs.GetRetryCandidatesAsync(Arg.Any<IReadOnlyCollection<JobType>>(), Arg.Any<DateTime>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([job]);
        _jobs.TryRequeueForRetryAsync(job.Id, Arg.Any<CancellationToken>()).Returns(true);
        _previews.RepublishJobAsync(job.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new InvalidOperationException("broker down")));

        await CreateSut().RetryFailedPreviewsAsync(Now, TestContext.Current.CancellationToken);

        await _jobs.Received(1).UpdateStatusAsync(job.Id, JobStatus.Failed, Arg.Any<int?>(),
            JobRecoveryService.PublishFailedError, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task orphaned_queued_previews_are_touched_then_republished()
    {
        var orphan = new Job { Id = Guid.NewGuid(), Type = JobType.DocumentPreview, Status = JobStatus.Queued };
        _jobs.GetOrphanedQueuedJobsAsync(Arg.Any<IReadOnlyCollection<JobType>>(), Now.AddMinutes(-30),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([orphan]);
        _previews.RepublishJobAsync(orphan.Id, Arg.Any<CancellationToken>()).Returns(true);

        await CreateSut().RepublishOrphanedPreviewsAsync(Now, TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _jobs.TouchAsync(orphan.Id, Arg.Any<CancellationToken>());
            _previews.RepublishJobAsync(orphan.Id, Arg.Any<CancellationToken>());
        });
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 10)]
    [InlineData(3, 40)]
    public void retry_delay_doubles_per_attempt(int retryCount, int expectedMinutes)
    {
        JobRecoveryService.RetryDelay(TimeSpan.FromMinutes(5), retryCount)
            .Should().Be(TimeSpan.FromMinutes(expectedMinutes));
    }
}
