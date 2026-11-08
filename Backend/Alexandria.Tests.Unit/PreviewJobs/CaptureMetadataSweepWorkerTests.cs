using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Workers.Media;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.PreviewJobs;

public class CaptureMetadataSweepWorkerTests
{
    private readonly ICaptureMetadataRepository _repository = Substitute.For<ICaptureMetadataRepository>();
    private readonly IContentStorageService _storage = Substitute.For<IContentStorageService>();
    private readonly ICaptureMetadataService _captureMetadata = Substitute.For<ICaptureMetadataService>();

    private CaptureMetadataSweepWorker CreateSut(int retryFailedAfterHours = 24)
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.CaptureMetadata.Returns(_repository);

        var services = new ServiceCollection();
        services.AddSingleton(unitOfWork);
        services.AddSingleton(_storage);
        services.AddSingleton(_captureMetadata);

        return new CaptureMetadataSweepWorker(
            NullLogger<CaptureMetadataSweepWorker>.Instance,
            Options.Create(new CaptureMetadataSweepConfig
            {
                BatchSize = 2,
                RetryFailedAfterHours = retryFailedAfterHours
            }),
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private void Candidates(params Guid[] versionIds)
    {
        _repository.GetExtractionCandidatesAsync(
                CaptureMetadataExtractor.CurrentVersion, 2,
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(versionIds);
    }

    [Fact]
    public async Task every_candidate_is_read_and_stored()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        Candidates(first, second);
        _storage.DownloadSeekableFile(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream());

        var stored = await CreateSut().SweepBatchAsync(TestContext.Current.CancellationToken);

        stored.Should().Be(2);
        await _captureMetadata.Received(1).ExtractAndStoreAsync(first, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _captureMetadata.Received(1).ExtractAndStoreAsync(second, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task nothing_to_do_returns_zero_without_touching_storage()
    {
        Candidates();

        var stored = await CreateSut().SweepBatchAsync(TestContext.Current.CancellationToken);

        stored.Should().Be(0);
        await _storage.DidNotReceive().DownloadSeekableFile(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task one_failed_version_does_not_stop_the_rest_of_the_batch()
    {
        var broken = Guid.NewGuid();
        var healthy = Guid.NewGuid();
        Candidates(broken, healthy);
        _storage.DownloadSeekableFile(healthy, Arg.Any<CancellationToken>()).Returns(new MemoryStream());
        _storage.DownloadSeekableFile(broken, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Stream>(new InvalidOperationException("object is gone")));

        var stored = await CreateSut().SweepBatchAsync(TestContext.Current.CancellationToken);

        stored.Should().Be(1);
        await _captureMetadata.Received(1).ExtractAndStoreAsync(healthy, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task failed_versions_are_excluded_so_a_corrupt_file_cannot_block_the_queue()
    {
        var broken = Guid.NewGuid();
        var excludedSets = new List<Guid[]>();
        _repository.GetExtractionCandidatesAsync(
                Arg.Any<int>(), Arg.Any<int>(),
                Arg.Do<IReadOnlyCollection<Guid>>(excluded => excludedSets.Add(excluded.ToArray())),
                Arg.Any<CancellationToken>())
            .Returns([broken], []);
        _storage.DownloadSeekableFile(broken, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Stream>(new InvalidOperationException("object is gone")));
        var sut = CreateSut();

        await sut.SweepBatchAsync(TestContext.Current.CancellationToken);
        await sut.SweepBatchAsync(TestContext.Current.CancellationToken);

        excludedSets[0].Should().BeEmpty();
        excludedSets[1].Should().Contain(broken);
    }

    [Fact]
    public async Task failures_are_retried_once_the_queue_is_empty_and_the_wait_has_passed()
    {
        var broken = Guid.NewGuid();
        var excludedSets = new List<Guid[]>();
        _repository.GetExtractionCandidatesAsync(
                Arg.Any<int>(), Arg.Any<int>(),
                Arg.Do<IReadOnlyCollection<Guid>>(excluded => excludedSets.Add(excluded.ToArray())),
                Arg.Any<CancellationToken>())
            .Returns([broken], [], [broken]);
        _storage.DownloadSeekableFile(broken, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Stream>(new InvalidOperationException("object is gone")));
        var sut = CreateSut(retryFailedAfterHours: 0);

        await sut.SweepBatchAsync(TestContext.Current.CancellationToken);
        await sut.SweepBatchAsync(TestContext.Current.CancellationToken);
        await sut.SweepBatchAsync(TestContext.Current.CancellationToken);

        excludedSets[1].Should().Contain(broken);
        excludedSets[2].Should().BeEmpty();
    }

    [Fact]
    public async Task shutdown_stops_the_batch_instead_of_marking_versions_failed()
    {
        var versionId = Guid.NewGuid();
        Candidates(versionId);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _storage.DownloadSeekableFile(versionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Stream>(new OperationCanceledException(cancelled.Token)));

        var act = () => CreateSut().SweepBatchAsync(cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
