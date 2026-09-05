using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Builders;
using Alexandria.Workers.MediaMetadata.Config;
using Alexandria.Workers.MediaMetadata.Workers;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.MediaMetadata;

public class TimeoutSweepWorkerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IEssentiaBatchRepository _batches = Substitute.For<IEssentiaBatchRepository>();
    private readonly IEssentiaBatchFileRepository _batchFiles = Substitute.For<IEssentiaBatchFileRepository>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly IFileEnrichmentRepository _enrichments = Substitute.For<IFileEnrichmentRepository>();
    private readonly TimeoutSweepWorker _sut;

    public TimeoutSweepWorkerTests()
    {
        _unitOfWork.EssentiaBatches.Returns(_batches);
        _unitOfWork.EssentiaBatchFiles.Returns(_batchFiles);
        _unitOfWork.Jobs.Returns(_jobs);
        _unitOfWork.FileEnrichments.Returns(_enrichments);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _sut = new TimeoutSweepWorker(
            Substitute.For<ILogger<TimeoutSweepWorker>>(),
            Options.Create(new EssentiaConfig { Backbone = "effnet" }),
            Substitute.For<IServiceProvider>());
    }

    private static (EssentiaBatch Batch, EssentiaBatchFile File, Job Job) StoredFile(JobStatus status)
    {
        var job = new JobBuilder().WithStatus(status).WithType(JobType.MetadataEnrichment).Build();
        var file = new EssentiaBatchFileBuilder().WithJob(job).WithFile(Guid.NewGuid()).Build();
        var batch = new EssentiaBatchBuilder()
            .WithBackbone(EssentiaBackbone.Effnet)
            .WithStatus(EssentiaBatchStatus.Dispatched)
            .WithFiles([file])
            .Build();
        file.BatchId = batch.Id;
        return (batch, file, job);
    }

    private IServiceScope NewScope()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        return scope;
    }

    [Fact]
    public async Task no_overdue_batches_is_noop()
    {
        _batches.GetDispatchedSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<EssentiaBatch>());

        await _sut.SweepAsync(NewScope(), TestContext.Current.CancellationToken);

        await _batches.DidNotReceive().MarkTimedOutAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task overdue_batch_with_pending_files_fails_only_those_jobs()
    {
        var (batch, pendingFile, pendingJob) = StoredFile(JobStatus.Queued);
        var (_, readyFile, readyJob) = StoredFile(JobStatus.Ready);
        readyFile.BatchId = batch.Id;
        batch.Files.Add(readyFile);
        _batches.GetDispatchedSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<EssentiaBatch> { batch });
        _batchFiles.GetByBatchAsync(batch.Id, Arg.Any<CancellationToken>())
            .Returns(new List<EssentiaBatchFile> { pendingFile, readyFile });

        await _sut.SweepAsync(NewScope(), TestContext.Current.CancellationToken);

        await _batches.Received(1).MarkTimedOutAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { batch.Id })),
            Arg.Any<CancellationToken>());
        await _jobs.Received(1).UpdateStatusForJobsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { pendingFile.JobId })),
            JobStatus.Failed, "batch timed out", Arg.Any<CancellationToken>());
        readyJob.Status.Should().Be(JobStatus.Ready);
        await _enrichments.Received(2).UpsertAsync(
            Arg.Any<FileEnrichment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task overdue_batch_without_pending_files_skips_file_updates()
    {
        var (batch, _, _) = StoredFile(JobStatus.Ready);
        _batches.GetDispatchedSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<EssentiaBatch> { batch });
        _batchFiles.GetByBatchAsync(batch.Id, Arg.Any<CancellationToken>())
            .Returns(new List<EssentiaBatchFile>());

        await _sut.SweepAsync(NewScope(), TestContext.Current.CancellationToken);

        await _batches.Received(1).MarkTimedOutAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
        await _jobs.DidNotReceive().UpdateStatusForJobsAsync(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<JobStatus>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _enrichments.DidNotReceiveWithAnyArgs().UpsertAsync(
            Arg.Any<FileEnrichment>(), Arg.Any<CancellationToken>());
    }
}