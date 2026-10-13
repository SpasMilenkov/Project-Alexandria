using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions;
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
    private readonly IPublisherService _publisher = Substitute.For<IPublisherService>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _versionId = Guid.NewGuid();

    public TranspilationCapabilityTests()
    {
        _unitOfWork.TranspilationJobs.Returns(_transpilations);
        _unitOfWork.Jobs.Returns(_jobs);
    }

    private TranspilationJobService CreateSut(bool enabled) => new(_unitOfWork, _publisher,
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
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!);
    }

    [Fact]
    public async Task CreateJobAsync_enabled_creates_and_dispatches_job()
    {
        var result = await CreateSut(true).CreateJobAsync(_versionId, _userId, [], [],
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(JobStatus.Queued);
        await _jobs.Received(1).AddAsync(Arg.Is<Job>(j => j.Type == JobType.Transpilation),
            TestContext.Current.CancellationToken);
        await _publisher.Received(1).PublishAsync(Arg.Any<byte[]>(), "transpilation.job");
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
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!);
    }

    [Fact]
    public async Task GetByIdAsync_disabled_still_reads_existing_representation_job()
    {
        var job = new TranspilationJob { Id = Guid.NewGuid(), Job = new Job { Status = JobStatus.Ready } };
        _transpilations.GetWithRepresentationsAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await CreateSut(false).GetByIdAsync(job.Id, TestContext.Current.CancellationToken);

        result.Status.Should().Be(JobStatus.Ready);
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!);
    }
}
