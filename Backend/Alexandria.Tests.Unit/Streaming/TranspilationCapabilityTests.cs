using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Streaming;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Streaming;

public class TranspilationCapabilityTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITranspilationJobRepository _transpilations = Substitute.For<ITranspilationJobRepository>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly IMessageBrokerStatus _broker = Substitute.For<IMessageBrokerStatus>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _versionId = Guid.NewGuid();

    public TranspilationCapabilityTests()
    {
        _unitOfWork.TranspilationJobs.Returns(_transpilations);
        _unitOfWork.Jobs.Returns(_jobs);
        _broker.IsEnabled.Returns(true);
        _broker.IsAvailable.Returns(true);
    }

    private TranspilationJobService CreateSut(bool enabled) => new(_unitOfWork, _outbox, _broker,
        Substitute.For<ILogger<TranspilationJobService>>(),
        Options.Create(new ProcessingCapabilities { AdaptiveStreaming = enabled }));

    [Fact]
    public async Task CreateJobAsync_disabled_rejects_before_persisting_or_publishing()
    {
        var act = () => CreateSut(false).CreateJobAsync(_versionId, _userId, [], [],
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<CapabilityUnavailableException>();

        await _jobs.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _transpilations.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _outbox.DidNotReceiveWithAnyArgs().EnqueueJobAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateJobAsync_enabled_creates_and_dispatches_job()
    {
        var result = await CreateSut(true).CreateJobAsync(_versionId, _userId, [], [],
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(JobStatus.Queued);
        await _jobs.Received(1).AddAsync(Arg.Is<Job>(j => j.Type == JobType.Transpilation),
            TestContext.Current.CancellationToken);
        await _outbox.Received(1).EnqueueJobAsync("transpilation.job", Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _jobs.AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
            _transpilations.AddAsync(Arg.Any<TranspilationJob>(), Arg.Any<CancellationToken>());
            _outbox.EnqueueJobAsync("transpilation.job", Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateStatusAsync_requeue_disabled_rejects_both_overloads_without_resetting_job(bool withRungs)
    {
        var job = new TranspilationJob
        {
            Id = Guid.NewGuid(), JobId = Guid.NewGuid(), UserId = _userId,
            Job = new Job { Status = JobStatus.Failed }
        };
        _transpilations.FirstOrDefaultAsync(Arg.Any<Expression<Func<TranspilationJob, bool>>>(),
            Arg.Any<CancellationToken>()).Returns(job);
        var sut = CreateSut(false);
        Func<Task> act;
        if (withRungs)
            act = () => sut.UpdateStatusAsync(job.Id, _userId, JobStatus.Queued,
                [AudioRung.Kbps96], [], TestContext.Current.CancellationToken);
        else
            act = () => sut.UpdateStatusAsync(job.Id, _userId, JobStatus.Queued,
                TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<CapabilityUnavailableException>();

        job.Job.Status.Should().Be(JobStatus.Failed);
        await _jobs.DidNotReceiveWithAnyArgs().UpdateStatusAsync(default, default, default, default, default);
        await _jobs.DidNotReceiveWithAnyArgs().TryRequeueFinishedAsync(default, TestContext.Current.CancellationToken);
        await _outbox.DidNotReceiveWithAnyArgs().EnqueueJobAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateStatusAsync_requeue_claims_the_finished_job_then_enqueues_in_one_transaction()
    {
        var job = FailedTranspilation();
        _jobs.TryRequeueFinishedAsync(job.JobId, Arg.Any<CancellationToken>()).Returns(true);

        await CreateSut(true).UpdateStatusAsync(job.Id, _userId, JobStatus.Queued,
            [AudioRung.Kbps96], [], TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _jobs.TryRequeueFinishedAsync(job.JobId, Arg.Any<CancellationToken>());
            _transpilations.UpdateDetailsAsync(job.Id, Arg.Any<string?>(), Arg.Any<AudioRung[]?>(),
                Arg.Any<VideoRung[]?>(), Arg.Any<CancellationToken>());
            _outbox.EnqueueJobAsync("transpilation.job", job.JobId, Arg.Any<CancellationToken>());
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task UpdateStatusAsync_requeue_of_a_job_a_worker_claimed_since_the_read_is_rejected()
    {
        var job = FailedTranspilation();
        _jobs.TryRequeueFinishedAsync(job.JobId, Arg.Any<CancellationToken>()).Returns(false);
        _jobs.GetStatusAsync(job.JobId, Arg.Any<CancellationToken>()).Returns(JobStatus.Processing);

        var act = () => CreateSut(true).UpdateStatusAsync(job.Id, _userId, JobStatus.Queued,
            [AudioRung.Kbps96], [], TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidTranspilationTransitionException>();
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _outbox.DidNotReceiveWithAnyArgs().EnqueueJobAsync(default!, default, TestContext.Current.CancellationToken);
    }

    private TranspilationJob FailedTranspilation()
    {
        var job = new TranspilationJob
        {
            Id = Guid.NewGuid(), JobId = Guid.NewGuid(), UserId = _userId,
            Job = new Job { Status = JobStatus.Failed }
        };
        _transpilations.FirstOrDefaultAsync(Arg.Any<Expression<Func<TranspilationJob, bool>>>(),
            Arg.Any<CancellationToken>()).Returns(job);

        return job;
    }

    [Fact]
    public async Task GetByIdAsync_disabled_still_reads_existing_representation_job()
    {
        var job = new TranspilationJob { Id = Guid.NewGuid(), Job = new Job { Status = JobStatus.Ready } };
        _transpilations.GetWithRepresentationsAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await CreateSut(false).GetByIdAsync(job.Id, TestContext.Current.CancellationToken);

        result.Status.Should().Be(JobStatus.Ready);
        await _outbox.DidNotReceiveWithAnyArgs().EnqueueJobAsync(default!, default, TestContext.Current.CancellationToken);
    }
}
