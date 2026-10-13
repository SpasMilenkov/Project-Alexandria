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

namespace Alexandria.Tests.Unit.MediaMetadata;

public class AccumulatorWorkerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IEssentiaBatchRepository _batches = Substitute.For<IEssentiaBatchRepository>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly IOperationalEventRepository _events = Substitute.For<IOperationalEventRepository>();
    private readonly IPublisherService _publisher = Substitute.For<IPublisherService>();
    private readonly AccumulatorBuffer _buffer = new();
    private readonly AccumulatorWorker _sut;
    private readonly ProcessingCapabilities _capabilities = new();

    private static readonly EssentiaConfig Config = new()
    {
        LocalVolumePath = "/data",
        VolumeMountPath = "/data",
        OutputDir = "/data/output"
    };

    public AccumulatorWorkerTests()
    {
        _unitOfWork.EssentiaBatches.Returns(_batches);
        _unitOfWork.Jobs.Returns(_jobs);
        _unitOfWork.OperationalEvents.Returns(_events);
        _batches.AddAsync(Arg.Any<EssentiaBatch>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<EssentiaBatch>());

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);
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
            _publisher,
            provider,
            Options.Create(_capabilities));
    }

    private static List<StagedFile> BatchOf(int count) =>
        Enumerable.Range(0, count)
            .Select(_ => new StagedFile(Guid.NewGuid(), "/data/in/file.mp3", "audio/mpeg"))
            .ToList();

    [Fact]
    public async Task FlushAsync_audio_analysis_disabled_does_not_create_durable_jobs_or_dispatch()
    {
        _capabilities.AudioAnalysis = false;

        await _sut.FlushAsync(BatchOf(1), Config, TestContext.Current.CancellationToken);

        await _batches.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!);
    }

    [Theory]
    [InlineData("effnet", EssentiaBackbone.Effnet)]
    [InlineData("maest", EssentiaBackbone.Maest)]
    public async Task FlushAsync_selected_backbone_persists_matching_identity_and_dispatches_matching_queue(
        string backbone, EssentiaBackbone expected)
    {
        var config = new EssentiaConfig { Backbone = backbone };

        await _sut.FlushAsync(BatchOf(1), config, TestContext.Current.CancellationToken);

        await _batches.Received(1).AddAsync(Arg.Is<EssentiaBatch>(b => b.Backbone == expected),
            TestContext.Current.CancellationToken);
        await _publisher.Received(1).PublishAsync(Arg.Any<byte[]>(), $"media-metadata.classify.audio.{backbone}");
    }

    [Fact]
    public async Task FlushAsync_unknown_backbone_rejects_before_persisting_or_dispatching()
    {
        var act = () => _sut.FlushAsync(BatchOf(1), new EssentiaConfig { Backbone = "unavailable" },
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _batches.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!);
    }

    [Fact]
    public async Task persist_failure_records_cycle_failure_and_skips_publish()
    {
        _batches.AddAsync(Arg.Any<EssentiaBatch>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EssentiaBatch>(new InvalidOperationException("db down")));

        await _sut.FlushAsync(BatchOf(2), Config,
            TestContext.Current.CancellationToken);

        await _events.Received(1).AddAsync(
            Arg.Is<OperationalEvent>(e => e.Code == OperationalEventCode.WorkerCycleFailure),
            Arg.Any<CancellationToken>());
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Fact]
    public async Task publish_failure_fails_jobs_and_records_cycle_failure()
    {
        _publisher.PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("broker down")));

        await _sut.FlushAsync(BatchOf(2), Config,
            TestContext.Current.CancellationToken);

        await _jobs.Received(1).UpdateStatusForJobsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == 2),
            JobStatus.Failed, "publish failed", Arg.Any<CancellationToken>());
        await _events.Received(1).AddAsync(
            Arg.Is<OperationalEvent>(e => e.Code == OperationalEventCode.WorkerCycleFailure),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task successful_flush_publishes_classify_request_and_releases_buffer()
    {
        var batch = BatchOf(2);
        foreach (var staged in batch)
            _buffer.TryEnqueue(staged).Should().Be(EnqueueResult.Added);

        byte[]? published = null;
        _publisher.PublishAsync(Arg.Do<byte[]>(b => published = b), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        await _sut.FlushAsync(batch, Config, TestContext.Current.CancellationToken);

        published.Should().NotBeNull();
        var request = JsonSerializer.Deserialize<ClassifyRequest>(published!);
        request.Should().NotBeNull();
        request!.Files.Should().HaveCount(2);
        await _publisher.Received(1).PublishAsync(
            Arg.Any<byte[]>(), "media-metadata.classify.audio.effnet");
        await _events.DidNotReceiveWithAnyArgs().AddAsync(
            Arg.Any<OperationalEvent>(), Arg.Any<CancellationToken>());
        foreach (var staged in batch)
            _buffer.TryEnqueue(staged).Should().Be(EnqueueResult.Added);
    }
}
