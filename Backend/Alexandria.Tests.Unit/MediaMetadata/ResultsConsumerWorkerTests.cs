using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Builders;
using Alexandria.Workers.MediaMetadata.Config;
using Alexandria.Workers.MediaMetadata.Messages;
using Alexandria.Workers.MediaMetadata.Queueing;
using Alexandria.Workers.MediaMetadata.Workers;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RabbitMQ.Client;
using IoDirectory = System.IO.Directory;
using IoFile = System.IO.File;

namespace Alexandria.Tests.Unit.MediaMetadata;

public class ResultsConsumerWorkerTests : IDisposable
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IEssentiaBatchRepository _batches = Substitute.For<IEssentiaBatchRepository>();
    private readonly IFileEnrichmentRepository _enrichments = Substitute.For<IFileEnrichmentRepository>();
    private readonly ResultsConsumerWorker _sut;
    private readonly string _tmpRoot = IoDirectory.CreateTempSubdirectory("results-consumer-tests").FullName;

    private static readonly EssentiaBackbone Backbone = EssentiaBackbone.Effnet;

    public ResultsConsumerWorkerTests()
    {
        _unitOfWork.EssentiaBatches.Returns(_batches);
        _unitOfWork.FileEnrichments.Returns(_enrichments);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);

        var essentiaOptions = Options.Create(new EssentiaConfig
        {
            LocalVolumePath = _tmpRoot,
            VolumeMountPath = "/data",
            StagedDir = Path.Combine(_tmpRoot, "staged"),
            OutputDir = "/data/output"
        });

        _sut = new ResultsConsumerWorker(
            Substitute.For<ILogger<ResultsConsumerWorker>>(),
            Substitute.For<IConnection>(),
            provider,
            Options.Create(new RabbitMqConsumerConfig()),
            essentiaOptions,
            Substitute.For<IConfiguration>(),
            Substitute.For<IAutoTagQueue>());
    }

    public void Dispose()
    {
        IoDirectory.Delete(_tmpRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private (EssentiaBatch Batch, Job Job, Guid FileId) StoredFile(JobStatus status = JobStatus.Queued)
    {
        var fileId = Guid.NewGuid();
        var job = new JobBuilder()
            .WithStatus(status)
            .WithType(JobType.MetadataEnrichment)
            .Build();
        var batchFile = new EssentiaBatchFileBuilder()
            .WithJob(job)
            .WithFile(fileId)
            .Build();
        var batch = new EssentiaBatchBuilder()
            .WithBackbone(Backbone)
            .WithStatus(EssentiaBatchStatus.Dispatched)
            .WithFiles([batchFile])
            .Build();
        return (batch, job, fileId);
    }

    private string OutputDirFor(Guid batchId)
    {
        var dir = Path.Combine(_tmpRoot, $"out-{batchId:N}");
        IoDirectory.CreateDirectory(dir);
        return dir;
    }

    private static CompletionMessage CompletionFor(Guid batchId, string? outputDir) => new()
    {
        BatchId = batchId,
        Backbone = "effnet",
        OutputDir = outputDir is null ? null : $"/data/{Path.GetFileName(outputDir)}"
    };

    [Fact]
    public async Task missing_output_dir_marks_all_jobs_failed()
    {
        var (batch, job, _) = StoredFile();
        _batches.GetWithFilesAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);

        await _sut.HandleCompletionAsync(
            CompletionFor(batch.Id, null), TestContext.Current.CancellationToken);

        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorDetail.Should().Contain("output directory missing");
        job.CompletedAt.Should().NotBeNull();
        batch.Status.Should().Be(EssentiaBatchStatus.Completed);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task malformed_output_marks_job_failed()
    {
        var (batch, job, fileId) = StoredFile();
        _batches.GetWithFilesAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        var dir = OutputDirFor(batch.Id);
        await IoFile.WriteAllTextAsync(Path.Combine(dir, $"{fileId}.json"), "{not json",
            TestContext.Current.CancellationToken);

        await _sut.HandleCompletionAsync(
            CompletionFor(batch.Id, dir), TestContext.Current.CancellationToken);

        job.Status.Should().Be(JobStatus.Failed);
        batch.Status.Should().Be(EssentiaBatchStatus.Completed);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task analyzer_failure_marks_job_failed_with_error_detail()
    {
        var (batch, job, fileId) = StoredFile();
        _batches.GetWithFilesAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        var dir = OutputDirFor(batch.Id);
        await IoFile.WriteAllTextAsync(Path.Combine(dir, $"{fileId}.json"),
            """{"success":false,"error":"model crashed"}""",
            TestContext.Current.CancellationToken);

        await _sut.HandleCompletionAsync(
            CompletionFor(batch.Id, dir), TestContext.Current.CancellationToken);

        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorDetail.Should().Be("model crashed");
        job.CompletedAt.Should().NotBeNull();
        batch.Status.Should().Be(EssentiaBatchStatus.Completed);
    }

    [Fact]
    public async Task success_upserts_enrichments_and_readies_job()
    {
        var (batch, job, fileId) = StoredFile();
        _batches.GetWithFilesAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        var dir = OutputDirFor(batch.Id);
        await IoFile.WriteAllTextAsync(Path.Combine(dir, $"{fileId}.json"),
            """{"success":true,"backbone":"effnet","genre":{"predictions":[{"label":"rock","score":0.9}]},"mood":{"happy":{"joy":0.8}},"model_versions":{"genre":"g1","mood":"m1"}}""",
            TestContext.Current.CancellationToken);

        await _sut.HandleCompletionAsync(
            CompletionFor(batch.Id, dir), TestContext.Current.CancellationToken);

        job.Status.Should().Be(JobStatus.Ready);
        job.ProgressPercent.Should().Be(100);
        job.CompletedAt.Should().NotBeNull();
        batch.Status.Should().Be(EssentiaBatchStatus.Completed);
        await _enrichments.Received(2).UpsertAsync(
            Arg.Any<FileEnrichment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task already_completed_batch_returns_early()
    {
        var (batch, _, _) = StoredFile();
        batch.Status = EssentiaBatchStatus.Completed;
        _batches.GetWithFilesAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);

        await _sut.HandleCompletionAsync(
            CompletionFor(batch.Id, null), TestContext.Current.CancellationToken);

        await _enrichments.DidNotReceiveWithAnyArgs().UpsertAsync(
            Arg.Any<FileEnrichment>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}