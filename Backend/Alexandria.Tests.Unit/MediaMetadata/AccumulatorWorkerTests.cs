using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Workers.MediaMetadata.Config;
using Alexandria.Workers.MediaMetadata.Messages;
using Alexandria.Workers.MediaMetadata.Queueing;
using Alexandria.Workers.MediaMetadata.Services;
using Alexandria.Workers.MediaMetadata.Workers;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using IoDirectory = System.IO.Directory;
using IoFile = System.IO.File;

namespace Alexandria.Tests.Unit.MediaMetadata;

public class AccumulatorWorkerTests : IDisposable
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IEssentiaBatchRepository _batches = Substitute.For<IEssentiaBatchRepository>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly IOperationalEventRepository _events = Substitute.For<IOperationalEventRepository>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly AccumulatorBuffer _buffer = new();
    private readonly AccumulatorWorker _sut;
    private readonly string _stagedDir = IoDirectory.CreateTempSubdirectory("accumulator-tests").FullName;
    private readonly EssentiaConfig _config;
    private readonly ProcessingCapabilities _capabilities = new();

    public AccumulatorWorkerTests()
    {
        _config = new EssentiaConfig
        {
            LocalVolumePath = "/data",
            VolumeMountPath = "/data",
            StagedDir = _stagedDir,
            OutputDir = "/data/output"
        };

        _unitOfWork.EssentiaBatches.Returns(_batches);
        _unitOfWork.Jobs.Returns(_jobs);
        _unitOfWork.OperationalEvents.Returns(_events);
        _batches.AddAsync(Arg.Any<EssentiaBatch>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<EssentiaBatch>());

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);
        serviceProvider.GetService(typeof(IOutbox)).Returns(_outbox);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);

        _sut = new AccumulatorWorker(
            Substitute.For<ILogger<AccumulatorWorker>>(),
            _buffer,
            Options.Create(new EssentiaConfig()),
            provider,
            Options.Create(_capabilities));
    }

    public void Dispose()
    {
        IoDirectory.Delete(_stagedDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private List<StagedFile> BatchOf(int count)
    {
        IoDirectory.CreateDirectory(StagingPaths.IncomingDirectory(_stagedDir));

        return Enumerable.Range(0, count)
            .Select(_ =>
            {
                var fileId = Guid.NewGuid();
                var path = StagingPaths.NewIncomingPath(_stagedDir, fileId, "audio/mpeg");
                IoFile.WriteAllText(path, "audio");
                return new StagedFile(fileId, path, "audio/mpeg");
            })
            .ToList();
    }

    private IEnumerable<string> StagedFiles() =>
        IoDirectory.EnumerateFiles(_stagedDir, "*", SearchOption.AllDirectories);

    private Task NothingEnqueued() => _outbox.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default!, TestContext.Current.CancellationToken);

    private void CaptureEnqueued(Action<byte[]> capture) =>
        _outbox.EnqueueAsync(Arg.Any<string>(), Arg.Do(capture), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

    private void FailEnqueue() =>
        _outbox.EnqueueAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("database down")));

    [Fact]
    public async Task FlushAsync_audio_analysis_disabled_does_not_create_durable_jobs_or_dispatch()
    {
        _capabilities.AudioAnalysis = false;

        await _sut.FlushAsync(BatchOf(1), _config, TestContext.Current.CancellationToken);

        await _batches.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await NothingEnqueued();
    }

    [Theory]
    [InlineData("effnet", EssentiaBackbone.Effnet)]
    [InlineData("maest", EssentiaBackbone.Maest)]
    public async Task FlushAsync_selected_backbone_persists_matching_identity_and_dispatches_matching_queue(
        string backbone, EssentiaBackbone expected)
    {
        var config = new EssentiaConfig
        {
            Backbone = backbone,
            LocalVolumePath = _config.LocalVolumePath,
            VolumeMountPath = _config.VolumeMountPath,
            StagedDir = _stagedDir,
            OutputDir = _config.OutputDir
        };

        await _sut.FlushAsync(BatchOf(1), config, TestContext.Current.CancellationToken);

        await _batches.Received(1).AddAsync(Arg.Is<EssentiaBatch>(b => b.Backbone == expected),
            TestContext.Current.CancellationToken);
        await _outbox.Received(1).EnqueueAsync(
            $"media-metadata.classify.audio.{backbone}", Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FlushAsync_unknown_backbone_rejects_before_persisting_or_dispatching()
    {
        var act = () => _sut.FlushAsync(BatchOf(1), new EssentiaConfig { Backbone = "unavailable", StagedDir = _stagedDir },
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _batches.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await NothingEnqueued();
    }

    [Fact]
    public async Task persist_failure_records_cycle_failure_and_skips_publish()
    {
        _batches.AddAsync(Arg.Any<EssentiaBatch>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EssentiaBatch>(new InvalidOperationException("db down")));

        await _sut.FlushAsync(BatchOf(2), _config,
            TestContext.Current.CancellationToken);

        await _events.Received(1).AddActiveIfAbsentAsync(
            Arg.Is<OperationalEvent>(e => e.Code == OperationalEventCode.WorkerCycleFailure),
            Arg.Any<CancellationToken>());
        await NothingEnqueued();
    }

    [Fact]
    public async Task enqueue_failure_rolls_back_the_batch_and_releases_triggers()
    {
        var batch = BatchOf(2);
        var settled = new List<bool>();
        foreach (var staged in batch)
            _buffer.TryEnqueue(staged, persisted =>
            {
                settled.Add(persisted);
                return Task.CompletedTask;
            });

        FailEnqueue();

        await _sut.FlushAsync(batch, _config, TestContext.Current.CancellationToken);

        // Nothing was committed, so the triggers are redelivered instead of the jobs being failed.
        settled.Should().Equal(false, false);
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _jobs.DidNotReceiveWithAnyArgs().UpdateStatusForJobsAsync(default!, default, default!,
            TestContext.Current.CancellationToken);
        await _events.Received(1).AddActiveIfAbsentAsync(
            Arg.Is<OperationalEvent>(e => e.Code == OperationalEventCode.WorkerCycleFailure),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task triggers_are_acked_only_after_the_batch_and_dispatch_commit()
    {
        var batch = BatchOf(1);
        var committedWhenSettled = new List<bool>();
        foreach (var staged in batch)
            _buffer.TryEnqueue(staged, _ =>
            {
                committedWhenSettled.Add(_unitOfWork.ReceivedCalls()
                    .Any(call => call.GetMethodInfo().Name == nameof(IUnitOfWork.CommitAsync)));
                return Task.CompletedTask;
            });

        await _sut.FlushAsync(batch, _config, TestContext.Current.CancellationToken);

        committedWhenSettled.Should().Equal(true);
        Received.InOrder(() =>
        {
            _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _batches.AddAsync(Arg.Any<EssentiaBatch>(), Arg.Any<CancellationToken>());
            _outbox.EnqueueAsync("media-metadata.classify.audio.effnet", Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>());
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task successful_flush_publishes_classify_request_and_releases_buffer()
    {
        var batch = BatchOf(2);
        foreach (var staged in batch)
            _buffer.TryEnqueue(staged).Should().Be(EnqueueResult.Added);

        byte[]? published = null;
        CaptureEnqueued(b => published = b);

        await _sut.FlushAsync(batch, _config, TestContext.Current.CancellationToken);

        published.Should().NotBeNull();
        var request = JsonSerializer.Deserialize<ClassifyRequest>(published!);
        request.Should().NotBeNull();
        request!.Files.Should().HaveCount(2);
        await _outbox.Received(1).EnqueueAsync(
            "media-metadata.classify.audio.effnet", Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceiveWithAnyArgs().AddActiveIfAbsentAsync(
            Arg.Any<OperationalEvent>(), Arg.Any<CancellationToken>());
        foreach (var staged in batch)
            _buffer.TryEnqueue(staged).Should().Be(EnqueueResult.Added);
    }

    [Fact]
    public async Task persist_failure_releases_files_so_later_triggers_are_accepted()
    {
        var batch = BatchOf(2);
        var settled = new List<bool>();
        foreach (var staged in batch)
            _buffer.TryEnqueue(staged, persisted =>
            {
                settled.Add(persisted);
                return Task.CompletedTask;
            }).Should().Be(EnqueueResult.Added);

        _batches.AddAsync(Arg.Any<EssentiaBatch>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EssentiaBatch>(new InvalidOperationException("db down")));

        await _sut.FlushAsync(batch, _config,
            TestContext.Current.CancellationToken);

        settled.Should().Equal(false, false);
        foreach (var staged in batch)
            _buffer.TryEnqueue(staged).Should().Be(EnqueueResult.Added);
    }

    [Fact]
    public async Task successful_persist_acknowledges_each_trigger()
    {
        var batch = BatchOf(2);
        var settled = new List<bool>();
        foreach (var staged in batch)
            _buffer.TryEnqueue(staged, persisted =>
            {
                settled.Add(persisted);
                return Task.CompletedTask;
            });

        await _sut.FlushAsync(batch, _config,
            TestContext.Current.CancellationToken);

        settled.Should().Equal(true, true);
    }

    [Fact]
    public async Task recording_failure_does_not_escape_flush()
    {
        _batches.AddAsync(Arg.Any<EssentiaBatch>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EssentiaBatch>(new InvalidOperationException("db down")));
        _events.TouchActiveEventAsync(Arg.Any<ServiceType>(), Arg.Any<OperationalEventCode>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("still down")));

        var act = () => _sut.FlushAsync(BatchOf(1), _config,
            TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task successful_flush_moves_inputs_into_batch_owned_directory()
    {
        var batch = BatchOf(2);

        byte[]? published = null;
        CaptureEnqueued(b => published = b);

        await _sut.FlushAsync(batch, _config,
            TestContext.Current.CancellationToken);

        var request = JsonSerializer.Deserialize<ClassifyRequest>(published!);
        var batchDir = StagingPaths.BatchDirectory(_stagedDir, request!.BatchId);
        var expected = batch.Select(f => Path.Combine(batchDir, $"{f.FileId}.mp3")).ToList();

        request.Files.Should().BeEquivalentTo(expected);
        StagedFiles().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task concurrent_batches_for_the_same_file_get_separate_inputs()
    {
        var fileId = Guid.NewGuid();
        IoDirectory.CreateDirectory(StagingPaths.IncomingDirectory(_stagedDir));

        var first = StagingPaths.NewIncomingPath(_stagedDir, fileId, "audio/mpeg");
        var second = StagingPaths.NewIncomingPath(_stagedDir, fileId, "audio/mpeg");
        await IoFile.WriteAllTextAsync(first, "version-1", TestContext.Current.CancellationToken);
        await IoFile.WriteAllTextAsync(second, "version-2", TestContext.Current.CancellationToken);

        var published = new List<byte[]>();
        CaptureEnqueued(published.Add);

        await _sut.FlushAsync([new StagedFile(fileId, first, "audio/mpeg")], _config, TestContext.Current.CancellationToken);
        await _sut.FlushAsync([new StagedFile(fileId, second, "audio/mpeg")], _config, TestContext.Current.CancellationToken);

        var inputs = published
            .Select(b => JsonSerializer.Deserialize<ClassifyRequest>(b)!.Files.Single())
            .ToList();

        inputs.Should().OnlyHaveUniqueItems();
        (await IoFile.ReadAllTextAsync(inputs[0], TestContext.Current.CancellationToken)).Should().Be("version-1");
        (await IoFile.ReadAllTextAsync(inputs[1], TestContext.Current.CancellationToken)).Should().Be("version-2");
    }

    [Fact]
    public async Task persist_failure_removes_staged_inputs()
    {
        _batches.AddAsync(Arg.Any<EssentiaBatch>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EssentiaBatch>(new InvalidOperationException("db down")));

        await _sut.FlushAsync(BatchOf(2), _config,
            TestContext.Current.CancellationToken);

        StagedFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task enqueue_failure_removes_staged_inputs()
    {
        FailEnqueue();

        await _sut.FlushAsync(BatchOf(2), _config,
            TestContext.Current.CancellationToken);

        StagedFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task missing_staged_input_abandons_batch_so_triggers_are_redelivered()
    {
        var batch = BatchOf(1);
        batch.Add(new StagedFile(Guid.NewGuid(), Path.Combine(_stagedDir, "incoming", "missing.mp3"), "audio/mpeg"));

        var settled = new List<bool>();
        foreach (var staged in batch)
            _buffer.TryEnqueue(staged, persisted =>
            {
                settled.Add(persisted);
                return Task.CompletedTask;
            });

        await _sut.FlushAsync(batch, _config,
            TestContext.Current.CancellationToken);

        settled.Should().Equal(false, false);
        StagedFiles().Should().BeEmpty();
        await _batches.DidNotReceiveWithAnyArgs().AddAsync(Arg.Any<EssentiaBatch>(), Arg.Any<CancellationToken>());
        await NothingEnqueued();
        await _events.Received(1).AddActiveIfAbsentAsync(
            Arg.Is<OperationalEvent>(e => e.Code == OperationalEventCode.WorkerCycleFailure),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void pending_file_is_reported_until_dispatched()
    {
        var staged = new StagedFile(Guid.NewGuid(), "/staged/in.mp3", "audio/mpeg");

        _buffer.IsPending(staged.FileId).Should().BeFalse();
        _buffer.TryEnqueue(staged);
        _buffer.IsPending(staged.FileId).Should().BeTrue();

        _buffer.MarkDispatched(staged.FileId);
        _buffer.IsPending(staged.FileId).Should().BeFalse();
    }
}
